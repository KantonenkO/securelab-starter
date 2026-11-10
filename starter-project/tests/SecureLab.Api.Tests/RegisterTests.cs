using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

// ЛР 03: T-01 (успішна реєстрація), T-02 (безпечні відмови), duplicate-контракт 409, зайві поля.
public sealed class RegisterTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static string Unique() => "reg" + Guid.NewGuid().ToString("N")[..8];

    private static object Body(string userName, string? email = null, string? displayName = null, string? password = null) => new
    {
        userName,
        email = email ?? userName + "@example.test",
        displayName = displayName ?? "Студент " + userName,
        password = password ?? TestPassword.Seed
    };

    private async Task<bool> UserExistsAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.FindByNameAsync(userName) is not null;
    }

    [Fact]
    public async Task T01_ValidRegistration_Returns201_SafeDto_AndUserCanLogin()
    {
        using var client = factory.CreateClient();
        var userName = Unique();
        using var response = await client.PostAsJsonAsync("/api/auth/register", Body(userName));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/me", response.Headers.Location?.OriginalString);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(new[] { "displayName", "id", "userName" },
            root.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(userName, root.GetProperty("userName").GetString());

        // Реєстрація не виконує автоматичний login, але новим обліковим записом можна увійти.
        using var anonymousMe = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMe.StatusCode);
        using var login = await factory.LoginAsync(userName, TestPassword.Seed);
        using var me = await login.GetAsync("/api/me");
        var roles = JsonDocument.Parse(await me.Content.ReadAsStringAsync()).RootElement.GetProperty("roles");
        Assert.Equal("Reporter", roles[0].GetString());
    }

    [Fact]
    public async Task T02_DuplicateRegistration_Returns409_WithoutRevealingWhatIsTaken()
    {
        using var client = factory.CreateClient();
        var userName = Unique();
        using var first = await client.PostAsJsonAsync("/api/auth/register", Body(userName));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var sameName = await client.PostAsJsonAsync("/api/auth/register", Body(userName, email: Unique() + "@example.test"));
        using var sameEmail = await client.PostAsJsonAsync("/api/auth/register", Body(Unique(), email: userName + "@example.test"));
        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        // Той самий зовнішній вигляд для обох причин дублювання.
        Assert.Equal(await ProblemShape.ReadAsync(sameName), await ProblemShape.ReadAsync(sameEmail));
        var json = await sameName.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestPassword.Seed, json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ab", "x@example.test", "Ім'я", "userName")]              // занадто коротке ім'я
    [InlineData("bad name!", "x@example.test", "Ім'я", "userName")]       // недозволені символи
    [InlineData("validname", "not-an-email", "Ім'я", "email")]            // хибний email
    [InlineData("validname", "x@example.test", "   ", "displayName")]     // порожнє відображуване ім'я
    public async Task T02_InvalidFields_Return400WithFieldKey_AndCreateNoAccount(string userName, string email,
        string displayName, string key)
    {
        using var client = factory.CreateClient();
        var unique = userName == "validname" ? Unique() : userName;
        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new { userName = unique, email, displayName, password = TestPassword.Seed });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("errors").TryGetProperty(key, out _));
        Assert.DoesNotContain(TestPassword.Seed, json, StringComparison.Ordinal);
        Assert.False(await UserExistsAsync(unique));
    }

    [Fact]
    public async Task T02_DisplayName121Chars_Returns400()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/register", Body(Unique(), displayName: new string('я', 121)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var boundary = await client.PostAsJsonAsync("/api/auth/register", Body(Unique(), displayName: new string('я', 120)));
        Assert.Equal(HttpStatusCode.Created, boundary.StatusCode);
    }

    [Fact]
    public async Task T02_WeakPassword_Returns400_WithoutEchoingPassword()
    {
        using var client = factory.CreateClient();
        var userName = Unique();
        const string weak = "short1!A";
        using var response = await client.PostAsJsonAsync("/api/auth/register", Body(userName, password: weak));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("errors").TryGetProperty("password", out _));
        Assert.DoesNotContain(weak, json, StringComparison.Ordinal);
        Assert.False(await UserExistsAsync(userName));
    }

    [Fact]
    public async Task ExtraFields_RoleAndId_AreRejected_AndNoAccountIsCreated()
    {
        using var client = factory.CreateClient();
        var userName = Unique();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName, email = userName + "@example.test", displayName = "Зайві поля", password = TestPassword.Seed,
            role = "Administrator", id = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await UserExistsAsync(userName));
    }
}
