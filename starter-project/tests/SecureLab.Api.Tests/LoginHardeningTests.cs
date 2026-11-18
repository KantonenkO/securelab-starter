using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

// ЛР 03 (добрий рівень): rate limit входу, події автентифікації; (відмінний): строк дії/втрата чинності сеансу, hasher.
public sealed class LoginHardeningTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

    private WebApplicationFactory<Program> Limited(ILoggerProvider? logs = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:LoginThrottle:PermitLimit", "3");
            builder.UseSetting("Auth:LoginThrottle:IpPermitLimit", "50");
            builder.UseSetting("Auth:LoginThrottle:Window", Window.ToString());
            if (logs is not null)
                builder.ConfigureServices(services => services.AddSingleton(logs));
        });

    [Fact]
    public async Task RateLimit_Returns429_SameForKnownAndUnknown_ThenRecovers_AndKeyIsPerAccount()
    {
        using var host = Limited();
        using var client = host.CreateClient();
        var ghost = "ghost" + Guid.NewGuid().ToString("N")[..6];

        for (var i = 0; i < 3; i++)
        {
            using var wrong = await client.PostAsJsonAsync("/api/auth/login", new { userName = "morgan", password = TestPassword.Other });
            using var unknown = await client.PostAsJsonAsync("/api/auth/login", new { userName = ghost, password = TestPassword.Other });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        }
        // Четверта спроба: 429 і для існуючого, і для вигаданого імені, з однаковою формою відповіді.
        using var deniedKnown = await client.PostAsJsonAsync("/api/auth/login", new { userName = "morgan", password = TestPassword.Other });
        using var deniedUnknown = await client.PostAsJsonAsync("/api/auth/login", new { userName = ghost, password = TestPassword.Other });
        Assert.Equal(HttpStatusCode.TooManyRequests, deniedKnown.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, deniedUnknown.StatusCode);
        Assert.NotNull(deniedKnown.Headers.RetryAfter);
        Assert.Equal(await ProblemShape.ReadAsync(deniedKnown), await ProblemShape.ReadAsync(deniedUnknown));
        // Правильний пароль теж не проходить, поки вікно не минуло (лічильник не залежить від результату).
        using var deniedCorrect = await client.PostAsJsonAsync("/api/auth/login", new { userName = "morgan", password = TestPassword.Seed });
        Assert.Equal(HttpStatusCode.TooManyRequests, deniedCorrect.StatusCode);

        // Ключ - «IP + ім'я»: інше ім'я з тієї самої адреси не заблоковане.
        using var otherAccount = await client.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = TestPassword.Seed });
        Assert.Equal(HttpStatusCode.NoContent, otherAccount.StatusCode);

        // Відновлення після вікна.
        await Task.Delay(Window + TimeSpan.FromMilliseconds(400));
        using var recovered = await client.PostAsJsonAsync("/api/auth/login", new { userName = "morgan", password = TestPassword.Seed });
        Assert.Equal(HttpStatusCode.NoContent, recovered.StatusCode);
    }

    private sealed class CapturingLogger : ILoggerProvider
    {
        public readonly List<string> Messages = [];
        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Messages);
        public void Dispose() { }

        private sealed class Sink(string category, List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (category == "SecureLab.Auth") lock (messages) messages.Add(formatter(state, exception));
            }
        }
    }

    [Fact]
    public async Task AuthEvents_AreWritten_WithoutCredentials()
    {
        var logs = new CapturingLogger();
        using var host = Limited(logs);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        using var failed = await client.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = TestPassword.Other });
        using var ok = await client.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = TestPassword.Seed });
        await client.UseAntiforgeryTokenAsync(); // ЛР 6: logout входить до cookie-периметра і вимагає request token
        using var logout = await client.PostAsync("/api/auth/logout", null);
        for (var i = 0; i < 4; i++)
            using (await client.PostAsJsonAsync("/api/auth/login", new { userName = "bob", password = TestPassword.Other })) { }

        string[] messages;
        lock (logs.Messages) messages = logs.Messages.ToArray();
        Assert.Contains(messages, m => m.StartsWith("auth.login.failed"));
        Assert.Contains(messages, m => m.StartsWith("auth.login.succeeded userId="));
        Assert.Contains(messages, m => m.StartsWith("auth.logout userId="));
        Assert.Contains(messages, m => m.StartsWith("auth.login.throttled"));
        // Жодна подія не містить паролів, cookie або hash.
        foreach (var message in messages)
        {
            Assert.DoesNotContain(TestPassword.Seed, message, StringComparison.Ordinal);
            Assert.DoesNotContain(TestPassword.Other, message, StringComparison.Ordinal);
            Assert.DoesNotContain(".AspNetCore.Identity", message, StringComparison.Ordinal);
            Assert.DoesNotContain("AQAAAA", message, StringComparison.Ordinal); // префікс Identity PasswordHasher v3
        }
    }

    [Fact]
    public async Task SecurityStampChange_InvalidatesOldCookie()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero)));
        var userName = "stamp" + Guid.NewGuid().ToString("N")[..8];
        using var anonymous = host.CreateClient();
        using var register = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            userName, email = userName + "@example.test", displayName = "Stamp", password = TestPassword.Seed
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using (var login = await client.PostAsJsonAsync("/api/auth/login", new { userName, password = TestPassword.Seed }))
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using (var before = await client.GetAsync("/api/me"))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // Серверна подія: адміністратор/користувач змінює пароль -> оновлюється SecurityStamp.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByNameAsync(userName))!;
            var changed = await users.ChangePasswordAsync(user, TestPassword.Seed, TestPassword.Other);
            Assert.True(changed.Succeeded);
        }

        using var after = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode); // стара cookie більше не діє
    }

    [Fact]
    public void PasswordHasher_ReportsRehashNeeded_ForWeakerStoredHash_AndSuccessForCurrent()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "hash-probe", DisplayName = "probe" };
        // «Старий» hash із меншою кількістю ітерацій PBKDF2, ніж у поточної конфігурації.
        var oldHasher = new PasswordHasher<ApplicationUser>(
            Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }));
        var currentHasher = new PasswordHasher<ApplicationUser>();
        var oldHash = oldHasher.HashPassword(user, TestPassword.Seed);
        var currentHash = currentHasher.HashPassword(user, TestPassword.Seed);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, currentHasher.VerifyHashedPassword(user, oldHash, TestPassword.Seed));
        Assert.Equal(PasswordVerificationResult.Success, currentHasher.VerifyHashedPassword(user, currentHash, TestPassword.Seed));
        Assert.Equal(PasswordVerificationResult.Failed, currentHasher.VerifyHashedPassword(user, currentHash, TestPassword.Other));
        Assert.NotEqual(oldHash, currentHash);
        Assert.DoesNotContain(TestPassword.Seed, currentHash, StringComparison.Ordinal);
    }
}
