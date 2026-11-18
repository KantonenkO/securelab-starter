using System.Net;
using System.Net.Http.Json;

namespace SecureLab.Api.Tests;

// ЛР 6, RL-REG-01 (F-04): POST /api/auth/register - публічний endpoint із відрізнюваними відповідями (201/409/400);
// без обмеження він дає безкоштовний account-creation flooding і enumeration. Обмеження за IP, до валідації тіла.
public sealed class RegisterRateLimitTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

    private static object Payload() => new
    {
        userName = "rl" + Guid.NewGuid().ToString("N")[..10], email = Guid.NewGuid().ToString("N")[..10] + "@example.test",
        displayName = "Rate Limit", password = TestPassword.Seed
    };

    [Fact]
    public async Task RL_REG_01_Quota_429_AndRecovery_AfterWindow()
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Register:PermitLimit", "3");
            builder.UseSetting("RateLimiting:Register:Window", Window.ToString());
        });
        using var client = host.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            using var created = await client.PostAsJsonAsync("/api/auth/register", Payload());
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        // Дублікат ім'я/email після вичерпання: 429 однаково для будь-якого тіла (не розкриває існування акаунта).
        using var denied = await client.PostAsJsonAsync("/api/auth/register",
            new { userName = "alice", email = "alice@example.test", displayName = "x", password = TestPassword.Seed });
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.NotNull(denied.Headers.RetryAfter);
        using var deniedInvalid = await client.PostAsJsonAsync("/api/auth/register", new { userName = "" });
        Assert.Equal(HttpStatusCode.TooManyRequests, deniedInvalid.StatusCode);

        await Task.Delay(Window + TimeSpan.FromMilliseconds(500));
        using var recovered = await client.PostAsJsonAsync("/api/auth/register", Payload());
        Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
    }
}
