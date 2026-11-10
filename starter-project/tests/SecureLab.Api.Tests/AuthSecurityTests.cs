using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

// ЛР 03: A-01..A-07 і S-01. Усі перевірки - integration scenarios через справжній HTTP pipeline.
public sealed class AuthSecurityTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static object NewIncident(string title, params (string Name, object Value)[] extra)
    {
        var body = new Dictionary<string, object>
        {
            ["title"] = title,
            ["description"] = "Навчальний опис інциденту для перевірки власника запису.",
            ["severity"] = "Low",
            ["occurredAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        foreach (var (name, value) in extra) body[name] = value;
        return body;
    }

    private async Task<Guid?> OwnerOfAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        return await db.Incidents.Where(item => item.Title == title)
            .Select(item => (Guid?)item.OwnerUserId).SingleOrDefaultAsync();
    }

    private async Task CleanupAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        await db.Incidents.Where(item => item.Title == title).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task A01_AnonymousPost_ToProtectedEndpoint_Returns401_NotRedirect()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        var title = $"A01-{Guid.NewGuid():N}";
        using var response = await client.PostAsJsonAsync("/api/incidents", NewIncident(title));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Empty(response.Headers.WwwAuthenticate);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(await OwnerOfAsync(title)); // функціонального результату немає
    }

    [Fact]
    public async Task A02_Login_ThenMe_ReturnsCurrentPrincipal_WithSafeFieldsOnly()
    {
        using var client = await factory.LoginAsync("alice", TestPassword.Seed);
        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(DbSeeder.AliceId, root.GetProperty("id").GetGuid());
        Assert.Equal("alice", root.GetProperty("userName").GetString());
        var names = root.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "displayName", "id", "roles", "userName" }, names);
    }

    [Fact]
    public async Task A03_ForeignXDemoUserIdHeader_DoesNotChangeCurrentUser()
    {
        using var client = await factory.LoginAsync("alice", TestPassword.Seed);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Demo-UserId", DbSeeder.BobId.ToString());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(DbSeeder.AliceId, root.GetProperty("id").GetGuid());

        // Без cookie заявлений id нічого не дає (початковий дефект наданого endpoint).
        using var anonymous = factory.CreateClient();
        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        anonymousRequest.Headers.Add("X-Demo-UserId", DbSeeder.BobId.ToString());
        using var anonymousResponse = await anonymous.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task A04_CreateWithoutOwner_TakesOwnerFromPrincipal()
    {
        var title = $"A04-{Guid.NewGuid():N}";
        try
        {
            using var client = await factory.LoginAsync("bob", TestPassword.Seed);
            using var response = await client.PostAsJsonAsync("/api/incidents", NewIncident(title));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(DbSeeder.BobId, await OwnerOfAsync(title));
        }
        finally { await CleanupAsync(title); }
    }

    [Fact]
    public async Task A05_ForgedOwnerUserId_IsIgnored_OwnerStaysPrincipal()
    {
        var title = $"A05-{Guid.NewGuid():N}";
        try
        {
            using var client = await factory.LoginAsync("bob", TestPassword.Seed);
            using var response = await client.PostAsJsonAsync("/api/incidents",
                NewIncident(title, ("ownerUserId", DbSeeder.AliceId), ("OwnerUserId", DbSeeder.AliceId)));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var owner = await OwnerOfAsync(title);
            Assert.Equal(DbSeeder.BobId, owner);
            Assert.NotEqual(DbSeeder.AliceId, owner);
            // Відповідь не розкриває власника.
            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.False(root.TryGetProperty("ownerUserId", out _));
        }
        finally { await CleanupAsync(title); }
    }

    [Fact]
    public async Task A06_Logout_ThenProtectedRequest_Returns401()
    {
        using var client = await factory.LoginAsync("alice", TestPassword.Seed);
        using (var before = await client.GetAsync("/api/auth/session-check"))
            Assert.Equal(HttpStatusCode.NoContent, before.StatusCode);

        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        // Основна cookie видаляється (минулий строк); інші Set-Cookie можуть бути, тому Single лише за назвою.
        var primary = logout.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(".AspNetCore.Identity.Application="));
        Assert.Contains("expires=Thu, 01 Jan 1970", primary, StringComparison.OrdinalIgnoreCase);

        using var after = await client.GetAsync("/api/auth/session-check");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        using var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task A07_UnknownUser_And_WrongPassword_GiveSameExternalResult()
    {
        using var client = factory.CreateClient();
        using var unknown = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "nobody" + Guid.NewGuid().ToString("N")[..6], password = TestPassword.Other });
        using var wrong = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "alice", password = TestPassword.Other });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
        Assert.Equal(unknown.Content.Headers.ContentType?.MediaType, wrong.Content.Headers.ContentType?.MediaType);
        // traceId може відрізнятися (діагностичний), решта стабільних полів має збігатися.
        Assert.Equal(await ProblemShape.ReadAsync(unknown), await ProblemShape.ReadAsync(wrong));
        Assert.False(unknown.Headers.Contains("Set-Cookie"));
        Assert.False(wrong.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task S01_Responses_DoNotLeakPasswordHashCookieOrToken()
    {
        using var client = await factory.LoginAsync("alice", TestPassword.Seed);
        var forbidden = new[] { "password", "passwordHash", "securityStamp", "concurrencyStamp", "token", "cookie", "email" };
        var root = JsonDocument.Parse(await client.GetStringAsync("/api/me")).RootElement;
        foreach (var property in root.EnumerateObject())
            Assert.DoesNotContain(property.Name, forbidden, StringComparer.OrdinalIgnoreCase);

        using var failed = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { userName = "alice", password = TestPassword.Other });
        var body = await failed.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestPassword.Other, body, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
    }
}
