using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

// ЛР 6 (добрий рівень), RL-SEARCH-01: rate limiting GET /api/incidents/search як захист від зловживання (не заміна authn/authz).
// Partition key = перевірений principal (user id). Квота/вікно задаються конфігурацією RateLimiting:Search.
public sealed class SearchRateLimitTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

    private WebApplicationFactory<Program> Limited() => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("RateLimiting:Search:PermitLimit", "3");
        builder.UseSetting("RateLimiting:Search:Window", Window.ToString());
    });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> host, string user)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { userName = user, password = TestPassword.Seed });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return client;
    }

    [Fact]
    public async Task RL_SEARCH_01_Quota_429_PartitionsAreIndependent_AndRecovers()
    {
        using var host = Limited();
        using var alice = await LoginAsync(host, "alice");
        using var bob = await LoginAsync(host, "bob");

        for (var i = 0; i < 3; i++)
        {
            using var ok = await alice.GetAsync("/api/incidents/search?q=usb");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        // Четверта спроба Alice: 429 + Retry-After + Problem Details без даних пошуку.
        using var denied = await alice.GetAsync("/api/incidents/search?q=usb");
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.NotNull(denied.Headers.RetryAfter);
        Assert.Equal("application/problem+json", denied.Content.Headers.ContentType?.MediaType);
        var problem = await ProblemShape.ReadAsync(denied);
        Assert.Equal(429, problem.Status);

        // Інший partition (Bob) не заблокований вичерпаною квотою Alice.
        using var bobOk = await bob.GetAsync("/api/incidents/search?q=usb");
        Assert.Equal(HttpStatusCode.OK, bobOk.StatusCode);
        // Маршрут без політики не обмежений: ліміт не «протікає» на інші endpoint-и.
        using var list = await alice.GetAsync("/api/incidents");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        // Відновлення після вікна.
        await Task.Delay(Window + TimeSpan.FromMilliseconds(500));
        using var recovered = await alice.GetAsync("/api/incidents/search?q=usb");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task RL_SEARCH_01_DoesNotReplaceAuthentication_AnonymousStays401()
    {
        using var host = Limited();
        using var anonymous = host.CreateClient();
        for (var i = 0; i < 6; i++)
        {
            using var response = await anonymous.GetAsync("/api/incidents/search?q=usb");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); // authentication лишається окремою перевіркою
        }
    }

    [Fact]
    public async Task RL_SEARCH_01_RateLimitedUser_DoesNotGainForeignData_AfterQuota()
    {
        using var host = Limited();
        using var bob = await LoginAsync(host, "bob");
        for (var i = 0; i < 3; i++)
            using (await bob.GetAsync("/api/incidents/search?q=usb")) { }
        using var denied = await bob.GetAsync("/api/incidents/search?q=usb");
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        // Авторизація окрема: чужий ресурс як і раніше 404 незалежно від квоти.
        using var foreign = await bob.GetAsync("/api/incidents/20000000-0000-0000-0000-000000000001");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
