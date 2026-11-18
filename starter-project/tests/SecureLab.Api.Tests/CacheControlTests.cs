using System.Net;

namespace SecureLab.Api.Tests;

// ЛР 6, CACHE-01 (F-03): відповіді автентифікованого API з персональними даними не повинні потрапляти в спільні/браузерні кеші.
public sealed class CacheControlTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/incidents")]
    [InlineData("/api/incidents/20000000-0000-0000-0000-000000000001")]
    [InlineData("/api/incidents/search?q=usb")]
    [InlineData("/api/incidents/severity-summary")]
    [InlineData("/api/incident-comments/30000000-0000-0000-0000-000000000001")]
    public async Task CACHE_01_AuthenticatedApiResponses_AreNoStore(string path)
    {
        using var alice = await factory.LoginAsync("alice", TestPassword.Seed, withAntiforgery: false);
        using var response = await alice.GetAsync(path);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
    }

    [Fact]
    public async Task CACHE_01_ErrorResponsesOfApi_AreNoStore_Too()
    {
        using var alice = await factory.LoginAsync("alice", TestPassword.Seed, withAntiforgery: false);
        using var invalid = await alice.GetAsync("/api/incidents?status=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("no-store", invalid.Headers.CacheControl?.ToString() ?? "");
    }

    [Fact]
    public async Task CACHE_01_StaticClient_IsNotAffected()
    {
        using var client = factory.CreateClient();
        using var page = await client.GetAsync("/auth-lab.html");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.DoesNotContain("no-store", page.Headers.CacheControl?.ToString() ?? "");
    }
}
