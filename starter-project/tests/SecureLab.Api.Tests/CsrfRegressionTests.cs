using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

// ЛР 6, REG-CSRF-01: cookie-authenticated змінювальні маршрути вимагають пару antiforgery-токенів.
// Два обов'язкові результати: (1) без валідного request token - exact 400 Problem Details і НЕЗМІННИЙ ресурс;
// (2) легітимний запит із валідною парою (cookie + header) - успіх і очікувана зміна стану.
// Передумови (ресурс) створюються напряму через DbContext, щоб arrange не залежав від перевірюваного захисту.
public sealed class CsrfRegressionTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private const string AntiforgeryTitle = "Antiforgery token is missing or invalid.";
    private static string Password => TestPassword.Seed;

    private sealed record State(string Title, string Description, string Status);

    private async Task<Guid> SeedIncidentAsync(Guid owner, string title, IncidentStatus status = IncidentStatus.New)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var now = DateTimeOffset.UtcNow;
        var incident = new Incident
        {
            Id = Guid.NewGuid(), OwnerUserId = owner, Title = title,
            Description = "Навчальний опис для REG-CSRF-01 (штучні дані).", Severity = IncidentSeverity.Low,
            Status = status, OccurredAtUtc = now.AddHours(-1), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    private async Task CleanupAsync(params Guid[] ids)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        await db.Incidents.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private static async Task<State?> ReadAsync(HttpClient reader, Guid id)
    {
        using var response = await reader.GetAsync($"/api/incidents/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return new State(root.GetProperty("title").GetString()!, root.GetProperty("description").GetString()!,
            root.GetProperty("status").GetString()!);
    }

    private static async Task AssertAntiforgeryRejectionAsync(HttpResponseMessage response)
    {
        // Один точний 4xx (не 500 і не 401/403): контракт помилки спільний для всіх маршрутів.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ProblemShape.ReadAsync(response);
        Assert.Equal(AntiforgeryTitle, problem.Title);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task REG_CSRF_01_Put_WithoutOrInvalidToken_Rejected_ResourceUnchanged_ThenValidPairSucceeds()
    {
        var id = await SeedIncidentAsync(DbSeeder.AliceId, "CSRF-original-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // «Браузер Alice»: автоматична auth cookie є, request token немає.
            using var alice = await factory.LoginAsync("alice", Password, withAntiforgery: false);
            var before = await ReadAsync(alice, id);
            Assert.NotNull(before);

            // 1) Без header.
            using (var noToken = await alice.PutAsJsonAsync($"/api/incidents/{id}", new { title = "CSRF-attacker-1" }))
                await AssertAntiforgeryRejectionAsync(noToken);
            Assert.Equal(before, await ReadAsync(alice, id));

            // 2) Невалідне значення токена.
            using (var invalid = await alice.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/incidents/{id}")
                   { Content = JsonContent.Create(new { title = "CSRF-attacker-2" }) }.WithToken("not-a-real-token")))
                await AssertAntiforgeryRejectionAsync(invalid);
            Assert.Equal(before, await ReadAsync(alice, id));

            // 3) Токен, виданий іншому сеансу (Bob): cookie-частина й identity не збігаються з Alice.
            using var bob = await factory.LoginAsync("bob", Password, withAntiforgery: false);
            var bobsToken = await bob.GetAntiforgeryTokenAsync();
            using (var foreign = await alice.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/incidents/{id}")
                   { Content = JsonContent.Create(new { title = "CSRF-attacker-3" }) }.WithToken(bobsToken)))
                await AssertAntiforgeryRejectionAsync(foreign);
            Assert.Equal(before, await ReadAsync(alice, id));

            // 4) Легітимний шлях: токен від того самого сеансу -> успіх і очікувана зміна.
            await alice.UseAntiforgeryTokenAsync();
            using (var ok = await alice.PutAsJsonAsync($"/api/incidents/{id}", new { title = "CSRF-legit-change" }))
                Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var after = await ReadAsync(alice, id);
            Assert.Equal("CSRF-legit-change", after!.Title);
            Assert.Equal(before.Description, after.Description);
        }
        finally { await CleanupAsync(id); }
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("TRIAGE")]
    [InlineData("CREATE")]
    [InlineData("LOGOUT")]
    public async Task REG_CSRF_01_EveryCookieMutation_WithoutToken_Rejected_StateUnchanged(string operation)
    {
        var marker = "CSRF-" + operation + "-" + Guid.NewGuid().ToString("N")[..8];
        var id = await SeedIncidentAsync(DbSeeder.AliceId, marker);
        try
        {
            var actor = operation == "TRIAGE" ? "morgan" : "alice";
            using var client = await factory.LoginAsync(actor, Password, withAntiforgery: false);
            using var reader = await factory.LoginAsync("morgan", Password, withAntiforgery: false); // бачить усі записи
            var before = await ReadAsync(reader, id);

            using var response = operation switch
            {
                "PUT" => await client.PutAsJsonAsync($"/api/incidents/{id}", new { title = "attacker" }),
                "DELETE" => await client.DeleteAsync($"/api/incidents/{id}"),
                "TRIAGE" => await client.PostAsJsonAsync($"/api/incidents/{id}/triage", new { reason = "attacker" }),
                "CREATE" => await client.PostAsJsonAsync("/api/incidents", new
                {
                    title = marker + "-created", description = "Атакувальний запис, який не має з'явитися.",
                    severity = "Low", occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
                }),
                _ => await client.PostAsync("/api/auth/logout", null)
            };
            await AssertAntiforgeryRejectionAsync(response);

            // Стан: ресурс незмінний, нових записів немає, сеанс після logout-CSRF усе ще активний.
            Assert.Equal(before, await ReadAsync(reader, id));
            if (operation == "CREATE")
            {
                using var search = await reader.GetAsync($"/api/incidents/search?q={marker}-created");
                Assert.Equal("[]", (await search.Content.ReadAsStringAsync()).Trim());
            }
            if (operation == "LOGOUT")
            {
                using var me = await client.GetAsync("/api/me");
                Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            }
        }
        finally { await CleanupAsync(id); }
    }

    [Theory]
    [InlineData("TRIAGE")]
    [InlineData("DELETE")]
    [InlineData("CREATE")]
    [InlineData("LOGOUT")]
    public async Task REG_CSRF_01_EveryCookieMutation_WithValidPair_Succeeds(string operation)
    {
        var marker = "CSRF-OK-" + operation + "-" + Guid.NewGuid().ToString("N")[..8];
        var id = await SeedIncidentAsync(DbSeeder.AliceId, marker);
        Guid created = Guid.Empty;
        try
        {
            var actor = operation == "TRIAGE" ? "morgan" : "alice";
            using var client = await factory.LoginAsync(actor, Password, withAntiforgery: true);
            switch (operation)
            {
                case "TRIAGE":
                    using (var triage = await client.PostAsJsonAsync($"/api/incidents/{id}/triage", new { reason = "перевірка" }))
                        Assert.Equal(HttpStatusCode.OK, triage.StatusCode);
                    Assert.Equal("Triaged", (await ReadAsync(client, id))!.Status);
                    break;
                case "DELETE":
                    using (var delete = await client.DeleteAsync($"/api/incidents/{id}"))
                        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
                    Assert.Null(await ReadAsync(client, id));
                    break;
                case "CREATE":
                    using (var create = await client.PostAsJsonAsync("/api/incidents", new
                    {
                        title = marker + "-created", description = "Легітимний запис із валідною парою токенів.",
                        severity = "Low", occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
                    }))
                    {
                        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
                        created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
                    }
                    Assert.NotNull(await ReadAsync(client, created));
                    break;
                default:
                    using (var logout = await client.PostAsync("/api/auth/logout", null))
                        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
                    using (var me = await client.GetAsync("/api/me"))
                        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
                    break;
            }
        }
        finally { await CleanupAsync(id, created); }
    }

    [Fact]
    public async Task TokenEndpoint_IsNotCacheable_AndIssuesAntiforgeryCookie()
    {
        using var client = await factory.LoginAsync("alice", Password, withAntiforgery: false);
        using var response = await client.GetAsync(AntiforgeryTestExtensions.TokenRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
    }
}
