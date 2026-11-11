using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SecureLab.Api.Tests;

// ЛР 03: фактичні атрибути cookie (значення cookie не читається й не друкується).
public sealed class CookieFlagsTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static string[] Attributes(HttpResponseMessage response)
    {
        var matching = response.Headers.GetValues("Set-Cookie")
            .Where(value => value.StartsWith(".AspNetCore.Identity.Application=")).ToArray();
        Assert.True(matching.Length == 1);
        // Перша частина - «назва=значення»: пропускаємо, щоб значення не потрапило у failure message.
        return matching[0].Split(';').Skip(1).Select(part => part.Trim().ToLowerInvariant()).ToArray();
    }

    [Fact]
    public async Task Login_OverHttps_SetsHttpOnly_Secure_SameSiteLax()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = TestPassword.Seed });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var attributes = Attributes(response);
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("path=/", attributes);
        Assert.DoesNotContain(attributes, a => a.StartsWith("domain="));
    }

    [Fact]
    public async Task Login_OverLocalHttpInDevelopment_HasNoSecure_ButKeepsHttpOnlyAndSameSite()
    {
        // Це межа локального HTTP: SameAsRequest не додає Secure, тому такий trace не доводить production-профіль.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = TestPassword.Seed });

        var attributes = Attributes(response);
        Assert.DoesNotContain("secure", attributes);
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
    }

    [Fact]
    public async Task Login_InProductionProfile_AlwaysSetsSecure_EvenOverHttp()
    {
        // Реальний production-профіль: Secure=Always не залежить від схеми запиту.
        var connection = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("SecureLab")!;
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:SecureLab", connection);
            builder.UseSetting("Auth:LoginThrottle:PermitLimit", "1000");
        });
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "alice", password = TestPassword.Seed });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var attributes = Attributes(response);
        Assert.Contains("secure", attributes);
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
    }
}
