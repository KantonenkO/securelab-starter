using System.Net.Http.Json;

namespace SecureLab.Api.Tests;

// ЛР 6: test-only helper для antiforgery. Контракт сервера - docs/security-review.md (розділ 8):
// маршрут видачі токена і header. Токен прив'язаний до principal, тому його запитують ПІСЛЯ login тим самим клієнтом.
public static class AntiforgeryTestExtensions
{
    public const string TokenRoute = "/api/antiforgery/token";
    public const string HeaderName = "X-CSRF-TOKEN";

    private sealed record TokenResponse(string Token);

    /// <summary>Повертає request token (cookie-частина лягає в cookie jar цього клієнта).</summary>
    public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client)
    {
        using var response = await client.GetAsync(TokenRoute);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (string.IsNullOrWhiteSpace(body?.Token))
            throw new InvalidOperationException("Missing request token.");
        return body.Token;
    }

    /// <summary>Зручний синтаксис: додає header токена лише для ЦЬОГО клієнта (не глобально для harness).</summary>
    public static async Task UseAntiforgeryTokenAsync(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(HeaderName);
        client.DefaultRequestHeaders.Add(HeaderName, await client.GetAntiforgeryTokenAsync());
    }

    public static HttpRequestMessage WithToken(this HttpRequestMessage request, string token)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.Add(HeaderName, token);
        return request;
    }
}
