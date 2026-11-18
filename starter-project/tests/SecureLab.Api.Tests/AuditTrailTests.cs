using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

// ЛР 6 (добрий рівень), AUD-01 (F-05): канонічний привілейований перехід Incident.Status (New -> Triaged)
// лишає audit event і для дозволеної дії, і для відмови: subject, action, resource, result, time (+ traceId).
// Free-text причини переходу, cookie, токени й тіло запиту в подію не потрапляють.
public sealed class AuditTrailTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private const string SecretReason = "ВРАЗЛИВИЙ-ТЕКСТ-ПРИЧИНИ-12345";

    private sealed class AuditCapture : ILoggerProvider
    {
        public readonly List<string> Lines = [];
        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Lines);
        public void Dispose() { }

        private sealed class Sink(string category, List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category == "SecureLab.Audit") lock (lines) lines.Add(formatter(state, exception));
            }
        }
    }

    private async Task<Guid> SeedNewIncidentAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var now = DateTimeOffset.UtcNow;
        var incident = new Incident
        {
            Id = Guid.NewGuid(), OwnerUserId = DbSeeder.AliceId, Title = "AUD-01-" + Guid.NewGuid().ToString("N")[..8],
            Description = "Навчальний опис для AUD-01 (штучні дані).", Severity = IncidentSeverity.Low,
            Status = IncidentStatus.New, OccurredAtUtc = now.AddHours(-1), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> host, string user)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { userName = user, password = TestPassword.Seed });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        await client.UseAntiforgeryTokenAsync();
        return client;
    }

    [Fact]
    public async Task AUD_01_Triage_AllowedAndDenied_WriteAuditEvents_WithRequiredFields_AndNoSensitiveData()
    {
        var logs = new AuditCapture();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        var allowedId = await SeedNewIncidentAsync(host);
        var deniedId = await SeedNewIncidentAsync(host);
        try
        {
            using var morgan = await LoginAsync(host, "morgan");   // Analyst: дозволений перехід
            using var alice = await LoginAsync(host, "alice");     // Reporter: відмова
            using var allowed = await morgan.PostAsJsonAsync($"/api/incidents/{allowedId}/triage", new { reason = SecretReason });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            using var denied = await alice.PostAsJsonAsync($"/api/incidents/{deniedId}/triage", new { reason = SecretReason });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            string[] lines;
            lock (logs.Lines) lines = logs.Lines.ToArray();

            var allowedLine = Assert.Single(lines, line => line.StartsWith("incident.status.changed"));
            Assert.Contains($"subject={DbSeeder.MorganId}", allowedLine);
            Assert.Contains("action=incident.triage", allowedLine);
            Assert.Contains($"resource=incident:{allowedId}", allowedLine);
            Assert.Contains("result=allowed", allowedLine);
            Assert.Contains("from=New to=Triaged", allowedLine);
            Assert.Matches(@"occurredAtUtc=\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", allowedLine);
            Assert.Contains("traceId=", allowedLine);

            var deniedLine = Assert.Single(lines, line => line.StartsWith("authorization.denied"));
            Assert.Contains($"subject={DbSeeder.AliceId}", deniedLine);
            Assert.Contains($"resource=incident:{deniedId}", deniedLine);
            Assert.Contains("result=denied", deniedLine);

            // Перехід відображено і в історії БД; відмова стан не змінила.
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
            Assert.Equal(IncidentStatus.Triaged, (await db.Incidents.AsNoTracking().SingleAsync(item => item.Id == allowedId)).Status);
            Assert.Equal(IncidentStatus.New, (await db.Incidents.AsNoTracking().SingleAsync(item => item.Id == deniedId)).Status);

            foreach (var line in lines)
            {
                Assert.DoesNotContain(SecretReason, line, StringComparison.Ordinal);
                Assert.DoesNotContain(TestPassword.Seed, line, StringComparison.Ordinal);
                Assert.DoesNotContain(".AspNetCore.Identity", line, StringComparison.Ordinal);
                Assert.DoesNotContain("SecureLab.Antiforgery", line, StringComparison.Ordinal);
            }
        }
        finally
        {
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
            await db.Incidents.Where(item => item.Id == allowedId || item.Id == deniedId).ExecuteDeleteAsync();
        }
    }
}
