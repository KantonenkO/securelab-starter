using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public static class CookieClient
{
    /// <param name="withAntiforgery">true - після login клієнт одержує request token і додає header до своїх запитів
    /// (шлях легітимного UI). false - «браузер із автоматичною cookie, але без токена» (негативна частина REG-CSRF-01).</param>
    public static async Task<HttpClient> LoginAsync(this SecureLabApiFactory factory, string userName, string password,
        bool withAntiforgery = false)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        if (!response.IsSuccessStatusCode)
        {
            client.Dispose();
            throw new InvalidOperationException("Study login failed (credentials omitted).");
        }
        if (withAntiforgery) await client.UseAntiforgeryTokenAsync();
        return client;
    }
}
