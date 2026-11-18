using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SecureLab.Api.Tests;

// ЛР 6 (етап 1 п. 4-5, етап 2 п. 1): production-like профіль із навмисно недосяжною БД (порт 54399, локальний, не інший host).
// ERR-01: безпечна 500 без внутрішніх деталей + traceId = TraceId запису журналу. HDR-01: безпечні заголовки є і на error-шляху.
public sealed class ErrorPathTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private sealed class ScopeCapturingProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
        public readonly List<string> Entries = [];
        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, this);
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
        public void Dispose() { }

        private sealed class Sink(string category, ScopeCapturingProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<string>();
                owner._scopes.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? ""), scopes);
                lock (owner.Entries) owner.Entries.Add($"{logLevel}|{category}|{string.Join(" => ", scopes)}");
            }
        }
    }

    private WebApplicationFactory<Program> ProductionWithBrokenDatabase(ILoggerProvider logs) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:SecureLab",
                "Host=127.0.0.1;Port=54399;Database=securelab;Username=securelab;"
                + "Password=unreachable-study-value;Include Error Detail=false");
            builder.ConfigureServices(services => services.AddSingleton(logs));
        });

    [Fact]
    public async Task ERR_01_HDR_01_UnexpectedError_IsSafe_HasSecurityHeaders_AndTraceMatchesLog()
    {
        var logs = new ScopeCapturingProvider();
        using var host = ProductionWithBrokenDatabase(logs);
        using var client = host.CreateClient();
        Assert.Equal("Production", host.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().EnvironmentName);

        // Звичайний успішний клієнт-запит: еталон заголовків (статична сторінка).
        using var page = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        // Публічний login з вигаданим паролем: недосяжна БД -> необроблений виняток -> спільний error handler.
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = "NotARealPassword1!" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        // Клієнт не бачить внутрішніх деталей.
        foreach (var forbidden in new[] { "Npgsql", "54399", "unreachable", "Exception", " at ", "SecureLab.Api", "Host=", "Password" })
            Assert.DoesNotContain(forbidden, body, StringComparison.Ordinal);

        // HDR-01: ті самі безпечні заголовки, що й на успішній відповіді.
        foreach (var name in new[] { "X-Content-Type-Options", "Referrer-Policy", "Content-Security-Policy" })
        {
            Assert.True(page.Headers.Contains(name), $"baseline 200 has no {name}");
            Assert.True(response.Headers.Contains(name), $"500 response has no {name}");
            Assert.Equal(page.Headers.GetValues(name).Single(), response.Headers.GetValues(name).Single());
        }
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());

        // ERR-01: середній сегмент W3C traceId відповіді дорівнює TraceId у scope запису журналу (без stack trace у відповіді).
        var traceparent = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("traceId").GetString()!;
        var traceId = Regex.Match(traceparent, "^00-([0-9a-f]{32})-").Groups[1].Value;
        Assert.NotEmpty(traceId);
        string[] entries;
        lock (logs.Entries) entries = logs.Entries.ToArray();
        Assert.Contains(entries, entry => entry.StartsWith("Error|Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware")
            && entry.Contains(traceId, StringComparison.OrdinalIgnoreCase));
    }
}
