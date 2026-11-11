using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

// ЛР 04: A-01…A-12 через HTTP boundary. Один користувач - один cookie-клієнт; негативні перевірки mutation
// порівнюють стан ДО і ПІСЛЯ відмови (прочитано дозволеним клієнтом). Власні ресурси створюються тестом і прибираються.
public sealed class AuthorizationTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static readonly Guid CommentToBob = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid InternalComment = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid PublicCommentToAlice = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static string Password => TestPassword.Seed;

    private sealed record State(string Title, string Description, string Status, string Severity, string Owner);

    private async Task<Guid> CreateAsync(HttpClient owner, string title, string severity = "Low")
    {
        using var response = await owner.PostAsJsonAsync("/api/incidents", new
        {
            title, description = "Навчальний опис інциденту для перевірки авторизації за ресурсом.", severity,
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private async Task CleanupAsync(params Guid[] ids)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        await db.Incidents.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private static async Task<State> ReadStateAsync(HttpClient reader, Guid id)
    {
        using var response = await reader.GetAsync($"/api/incidents/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return new State(root.GetProperty("title").GetString()!, root.GetProperty("description").GetString()!,
            root.GetProperty("status").GetString()!, root.GetProperty("severity").GetString()!,
            root.GetProperty("ownerDisplayName").GetString()!);
    }

    private static async Task<JsonObject> ProblemWithoutTraceAsync(HttpResponseMessage response)
    {
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        body.Remove("traceId"); body.Remove("requestId"); // єдині дозволені відмінності - поля кореляції запиту
        return body;
    }

    // ---------------------------------------------------------------- A-01
    [Theory]
    [InlineData("GET", "/api/incidents")]
    [InlineData("GET", "/api/incidents/20000000-0000-0000-0000-000000000002")]
    [InlineData("PUT", "/api/incidents/20000000-0000-0000-0000-000000000002")]
    [InlineData("DELETE", "/api/incidents/20000000-0000-0000-0000-000000000002")]
    [InlineData("POST", "/api/incidents/20000000-0000-0000-0000-000000000002/triage")]
    [InlineData("GET", "/api/incident-comments/30000000-0000-0000-0000-000000000002")]
    [InlineData("GET", "/api/incidents/search?q=usb")]
    [InlineData("GET", "/api/incidents/severity-summary")]
    public async Task A01_Anonymous_ProtectedEndpoints_Return401_NotRedirect(string method, string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "PUT" or "POST") request.Content = JsonContent.Create(new { title = "x" });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    // ---------------------------------------------------------------- A-02
    [Fact]
    public async Task A02_Owner_ReadsOwnIncident_Allow()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var response = await alice.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(DbSeeder.AliceIncidentId, root.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A02_Owner_UpdatesOwnContent_Allow_OnlyAllowedFieldsChange()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        var id = await CreateAsync(alice, $"A02-{Guid.NewGuid():N}");
        try
        {
            var before = await ReadStateAsync(alice, id);
            using var response = await alice.PutAsJsonAsync($"/api/incidents/{id}", new { title = "A02 змінено власником" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var after = await ReadStateAsync(alice, id);
            Assert.Equal("A02 змінено власником", after.Title);
            Assert.Equal(before with { Title = after.Title }, after); // owner, status, severity, description не змінились
        }
        finally { await CleanupAsync(id); }
    }

    // ---------------------------------------------------------------- A-03 / A-03R / A-10
    [Fact]
    public async Task A03_NonOwner_ReadsForeignIncident_Returns404_AndNoForeignDataInBody()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var response = await alice.GetAsync($"/api/incidents/{DbSeeder.BobIncidentId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Невідома спроба входу", body, StringComparison.Ordinal); // title Bob
        Assert.DoesNotContain("Боб", body, StringComparison.Ordinal);
        Assert.DoesNotContain(DbSeeder.BobIncidentId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A03R_Analyst_ReadsAliceIncident_Allow_ButCannotChangeContent()
    {
        using var morgan = await factory.LoginAsync("morgan", Password);
        using var read = await morgan.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // Право читання не дає права довільної зміни: 403 (ресурс видимий), стан без змін.
        var before = await ReadStateAsync(morgan, DbSeeder.AliceIncidentId);
        using var update = await morgan.PutAsJsonAsync($"/api/incidents/{DbSeeder.AliceIncidentId}", new { title = "змінено аналітиком" });
        using var delete = await morgan.DeleteAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(before, await ReadStateAsync(morgan, DbSeeder.AliceIncidentId));
    }

    [Fact]
    public async Task A10_ForeignAndUnknownIdentifier_GiveIdenticalProblem404()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        foreach (var (method, suffix) in new[] { ("GET", ""), ("PUT", ""), ("DELETE", ""), ("POST", "/triage") })
        {
            using var foreign = await Send(alice, method, $"/api/incidents/{DbSeeder.BobIncidentId}{suffix}");
            using var unknown = await Send(alice, method, $"/api/incidents/{Guid.NewGuid()}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal(unknown.StatusCode, foreign.StatusCode);
            Assert.Equal(unknown.Content.Headers.ContentType?.ToString(), foreign.Content.Headers.ContentType?.ToString());
            Assert.True(JsonNode.DeepEquals(await ProblemWithoutTraceAsync(unknown), await ProblemWithoutTraceAsync(foreign)),
                $"{method}{suffix}: тіла відрізняються не лише traceId");
        }
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "PUT") request.Content = JsonContent.Create(new { title = "x" });
        if (method is "POST") request.Content = JsonContent.Create(new { });
        return client.SendAsync(request);
    }

    // ---------------------------------------------------------------- A-04
    [Fact]
    public async Task A04_NonOwner_UpdateAndDeleteForeign_Return404_AndDoNotChangeResource()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var bob = await factory.LoginAsync("bob", Password);
        var id = await CreateAsync(bob, $"A04-{Guid.NewGuid():N}");
        try
        {
            var before = await ReadStateAsync(bob, id);
            using var update = await alice.PutAsJsonAsync($"/api/incidents/{id}", new { title = "змінено Alice", description = "змінено Alice" });
            using var delete = await alice.DeleteAsync($"/api/incidents/{id}");

            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            Assert.Equal(before, await ReadStateAsync(bob, id)); // Bob бачить свій ресурс без змін
        }
        finally { await CleanupAsync(id); }
    }

    [Fact]
    public async Task A04_Owner_Delete_Allowed_OnlyWhileNew()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var morgan = await factory.LoginAsync("morgan", Password);
        var removable = await CreateAsync(alice, $"A04d-{Guid.NewGuid():N}");
        var triaged = await CreateAsync(alice, $"A04t-{Guid.NewGuid():N}");
        try
        {
            using var del = await alice.DeleteAsync($"/api/incidents/{removable}");
            Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
            using var gone = await alice.GetAsync($"/api/incidents/{removable}");
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

            using var triage = await morgan.PostAsJsonAsync($"/api/incidents/{triaged}/triage", new { });
            Assert.Equal(HttpStatusCode.OK, triage.StatusCode);
            using var denied = await alice.DeleteAsync($"/api/incidents/{triaged}"); // власний, але не New
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("Triaged", (await ReadStateAsync(alice, triaged)).Status);
        }
        finally { await CleanupAsync(removable, triaged); }
    }

    // ---------------------------------------------------------------- A-05
    [Fact]
    public async Task A05_List_ReturnsOnlyAllowedSet_PerRole()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var morgan = await factory.LoginAsync("morgan", Password);
        using var admin = await factory.LoginAsync("admin", Password);
        var aliceIds = (await alice.GetFromJsonAsync<JsonArray>("/api/incidents"))!.Select(n => n!["id"]!.GetValue<Guid>()).ToArray();
        var morganIds = (await morgan.GetFromJsonAsync<JsonArray>("/api/incidents"))!.Select(n => n!["id"]!.GetValue<Guid>()).ToArray();
        var adminIds = (await admin.GetFromJsonAsync<JsonArray>("/api/incidents"))!.Select(n => n!["id"]!.GetValue<Guid>()).ToArray();

        Assert.Contains(DbSeeder.AliceIncidentId, aliceIds);
        Assert.DoesNotContain(DbSeeder.BobIncidentId, aliceIds);
        Assert.DoesNotContain(Guid.Parse("20000000-0000-0000-0000-000000000004"), aliceIds); // інцидент Bob з пошукового seed
        Assert.Contains(DbSeeder.BobIncidentId, morganIds); // Analyst: усі
        Assert.Equal(5, morganIds.Length);
        Assert.Empty(adminIds); // Administrator: прав «на все» немає, власних інцидентів немає
    }

    // ---------------------------------------------------------------- A-06 / A-07 / A-09
    [Fact]
    public async Task A06_Reporter_Triage_IsDenied_StatusUnchanged()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var bob = await factory.LoginAsync("bob", Password);
        var id = await CreateAsync(alice, $"A06-{Guid.NewGuid():N}");
        try
        {
            using var own = await alice.PostAsJsonAsync($"/api/incidents/{id}/triage", new { });          // власний ресурс, але не Analyst
            using var foreign = await bob.PostAsJsonAsync($"/api/incidents/{id}/triage", new { });         // чужий: невидимий
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal("New", (await ReadStateAsync(alice, id)).Status);
        }
        finally { await CleanupAsync(id); }
    }

    [Fact]
    public async Task A07_Analyst_Triage_NewIncident_Allowed_ServerSetsNextStatus_AndSecondAttemptConflicts()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var morgan = await factory.LoginAsync("morgan", Password);
        var id = await CreateAsync(alice, $"A07-{Guid.NewGuid():N}");
        try
        {
            using var forged = await morgan.PostAsJsonAsync($"/api/incidents/{id}/triage", new { status = "Closed" });
            Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode); // status з тіла не приймається
            Assert.Equal("New", (await ReadStateAsync(alice, id)).Status);

            using var ok = await morgan.PostAsJsonAsync($"/api/incidents/{id}/triage", new { reason = "Первинна класифікація" });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Equal("Triaged", (await ReadStateAsync(alice, id)).Status);

            using var again = await morgan.PostAsJsonAsync($"/api/incidents/{id}/triage", new { });
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode); // лише New → Triaged
            Assert.Equal("Triaged", (await ReadStateAsync(alice, id)).Status);
        }
        finally { await CleanupAsync(id); }
    }

    private sealed class CapturingLogger : ILoggerProvider
    {
        public readonly List<string> Audit = [];
        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Audit);
        public void Dispose() { }

        private sealed class Sink(string category, List<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category == "SecureLab.Audit") lock (sink) sink.Add(formatter(state, exception));
            }
        }
    }

    [Fact]
    public async Task A09_DeniedPrivilegedAction_WritesSafeAuditEvent_WithSameTraceIdAsResponse()
    {
        var logs = new CapturingLogger();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using var alice = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using (var login = await alice.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = Password }))
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var response = await alice.PostAsJsonAsync($"/api/incidents/{DbSeeder.AliceIncidentId}/triage", new { reason = "спроба" });
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string line;
        lock (logs.Audit) line = Assert.Single(logs.Audit);
        Assert.StartsWith("authorization.denied", line);
        Assert.Contains($"subject={DbSeeder.AliceId}", line);
        Assert.Contains("action=incident.triage", line);
        Assert.Contains($"resource=incident:{DbSeeder.AliceIncidentId}", line);
        Assert.Contains("result=denied", line);
        Assert.Contains("occurredAtUtc=", line);
        Assert.Contains($"traceId={body.GetProperty("traceId").GetString()}", line); // той самий кореляційний id
        foreach (var forbidden in new[] { Password, ".AspNetCore", "Cookie", "спроба", "StackTrace" })
            Assert.DoesNotContain(forbidden, line, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- A-08b
    [Fact]
    public async Task A08b_ForgedPrivilegedFields_InRegularUpdate_AreRejected_AndProtectedStateUnchanged()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        var id = await CreateAsync(alice, $"A08-{Guid.NewGuid():N}");
        try
        {
            var before = await ReadStateAsync(alice, id);
            foreach (var forged in new object[]
                     {
                         new { title = "A08 змінено", ownerUserId = DbSeeder.BobId },
                         new { title = "A08 змінено", status = "Resolved" },
                         new { title = "A08 змінено", severity = "Critical" },
                         new { title = "A08 змінено", role = "Administrator" }
                     })
            {
                using var response = await alice.PutAsJsonAsync($"/api/incidents/{id}", forged);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // контракт явно відхиляє зайве поле
            }
            Assert.Equal(before, await ReadStateAsync(alice, id)); // весь стан без змін
        }
        finally { await CleanupAsync(id); }
    }

    // ---------------------------------------------------------------- A-11
    [Fact]
    public async Task A11_SearchAndSeveritySummary_AreScopedToAllowedSet()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var morgan = await factory.LoginAsync("morgan", Password);

        // Seed: Alice - 1 Medium, 2 Low; Bob - 1 High, 1 Low. Агрегат Alice не містить записів Bob.
        var aliceSummary = (await alice.GetFromJsonAsync<JsonArray>("/api/incidents/severity-summary"))!
            .ToDictionary(n => n!["severity"]!.GetValue<string>(), n => n!["count"]!.GetValue<int>());
        Assert.Equal(1, aliceSummary["Medium"]);
        Assert.Equal(2, aliceSummary["Low"]);
        Assert.Equal(0, aliceSummary["High"]);
        var analystSummary = (await morgan.GetFromJsonAsync<JsonArray>("/api/incidents/severity-summary"))!
            .ToDictionary(n => n!["severity"]!.GetValue<string>(), n => n!["count"]!.GetValue<int>());
        Assert.Equal(1, analystSummary["High"]);
        Assert.Equal(3, analystSummary["Low"]);

        var aliceSearch = await alice.GetFromJsonAsync<JsonArray>("/api/incidents/search?q=O%27Brien");
        Assert.Empty(aliceSearch!); // інцидент Bob (…0004) більше не розкривається
        var analystSearch = await morgan.GetFromJsonAsync<JsonArray>("/api/incidents/search?q=O%27Brien");
        Assert.Single(analystSearch!);
        var own = await alice.GetFromJsonAsync<JsonArray>("/api/incidents/search?q=USB");
        Assert.Single(own!); // власний запис (…0005) знаходиться
    }

    // ---------------------------------------------------------------- A-12
    [Fact]
    public async Task A12_Child_DirectAccess_IsScopedViaParent_AndInternalCommentIsHiddenFromReporter()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        using var morgan = await factory.LoginAsync("morgan", Password);

        using var toBob = await alice.GetAsync($"/api/incident-comments/{CommentToBob}");     // коментар до інциденту Bob
        using var internalOwn = await alice.GetAsync($"/api/incident-comments/{InternalComment}"); // internal до власного
        using var publicOwn = await alice.GetAsync($"/api/incident-comments/{PublicCommentToAlice}");
        Assert.Equal(HttpStatusCode.NotFound, toBob.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, internalOwn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publicOwn.StatusCode);
        Assert.DoesNotContain("Розпочато перевірку", await toBob.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Details-проєкція: internal не потрапляє до Reporter, а Analyst його бачить після авторизації до parent.
        var aliceDetails = await alice.GetStringAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.DoesNotContain("Внутрішня примітка", aliceDetails, StringComparison.Ordinal);
        var morganDetails = await morgan.GetStringAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Contains("Внутрішня примітка", morganDetails, StringComparison.Ordinal);
        using var analystInternal = await morgan.GetAsync($"/api/incident-comments/{InternalComment}");
        Assert.Equal(HttpStatusCode.OK, analystInternal.StatusCode);
    }
}
