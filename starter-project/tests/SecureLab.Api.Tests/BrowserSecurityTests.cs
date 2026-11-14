using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

// ЛР 5: REG-CORS-01 (HTTP-частина), CSP/browser headers і відсутність небезпечних sink-ів у власному клієнті.
// Браузерна частина (DOM) - tests/browser/lab05-dom.js; тут лише те, що перевіряється без браузера.
public sealed class BrowserSecurityTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private const string Allowed = "http://localhost:4173", Disallowed = "http://localhost:4174";
    private const string Marker =
        "<img src=x onerror=\"document.querySelector('#lab05-marker-target').setAttribute('data-lab05-marker','executed')\">";

    private static HttpRequestMessage Get(string path, string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (origin is not null) request.Headers.Add("Origin", origin);
        return request;
    }

    private static string[] Values(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.ToArray() : [];

    // ------------------------------------------------------------ REG-CORS-01 (HTTP evidence)
    [Fact]
    public async Task Cors_AllowedOrigin_GetsExactAllowOrigin_AndCredentials_DisallowedGetsNoCorsHeaders()
    {
        using var client = factory.CreateClient();
        using var allowed = await client.SendAsync(Get("/health", Allowed));
        using var disallowed = await client.SendAsync(Get("/health", Disallowed));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(new[] { Allowed }, Values(allowed, "Access-Control-Allow-Origin"));
        Assert.Equal(new[] { "true" }, Values(allowed, "Access-Control-Allow-Credentials"));
        // Недозволений origin: сервер запит виконує (200), але жодного Access-Control-* немає: читати відповідь браузер не дозволить.
        Assert.Equal(HttpStatusCode.OK, disallowed.StatusCode);
        Assert.DoesNotContain(disallowed.Headers, h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cors_Preflight_AllowedOrigin_ListsExactMethodAndHeader_Disallowed_GetsNone()
    {
        using var client = factory.CreateClient();
        HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/health?probe=2");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "x-lab-cors-probe");
            return request;
        }
        using var allowed = await client.SendAsync(Preflight(Allowed));
        using var disallowed = await client.SendAsync(Preflight(Disallowed));

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(new[] { Allowed }, Values(allowed, "Access-Control-Allow-Origin"));
        Assert.Equal(new[] { "GET" }, Values(allowed, "Access-Control-Allow-Methods"));
        Assert.Equal(new[] { "X-Lab-Cors-Probe" }, Values(allowed, "Access-Control-Allow-Headers"));
        Assert.DoesNotContain(disallowed.Headers, h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cors_ProtectedEndpoint_WithoutCookie_Returns401_WithCorsHeadersForAllowedOrigin()
    {
        // UseCors стоїть до UseAuthorization: JavaScript allowed origin може прочитати саме відмову 401.
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Get("/api/incidents", Allowed));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(new[] { Allowed }, Values(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_IsNotAuthorization_DirectClientWithForeignOriginStillNeedsLogin()
    {
        using var client = factory.CreateClient();
        using var anonymous = await client.SendAsync(Get("/api/incidents", Disallowed));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); // request дійшов, API його відхилило само

        using var alice = await factory.LoginAsync("alice", TestPassword.Seed);
        using var authorized = await alice.SendAsync(Get("/api/incidents", Disallowed));
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode); // CORS не зупиняє direct HTTP client
        Assert.DoesNotContain(authorized.Headers, h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cors_Policy_IsNotRegisteredOutsideDevelopment()
    {
        var connection = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("SecureLab")!;
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:SecureLab", connection);
        });
        using var client = production.CreateClient();
        using var response = await client.SendAsync(Get("/health", Allowed));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(response.Headers, h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------ CSP і browser headers
    [Theory]
    [InlineData("/")]
    [InlineData("/browser-lab.html")]
    [InlineData("/auth-lab.html")]
    public async Task ClientPages_HaveEnforcedCsp_WithoutUnsafeInline_AndBaselineHeaders(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csp = Assert.Single(Values(response, "Content-Security-Policy"));
        Assert.Empty(Values(response, "Content-Security-Policy-Report-Only")); // кінцевий стан - enforcement, не два суперечливі headers
        foreach (var directive in new[] { "default-src 'self'", "script-src 'self'", "style-src 'self'", "img-src 'self'",
                     "connect-src 'self'", "frame-ancestors 'none'", "object-src 'none'" })
            Assert.Contains(directive, csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.DoesNotContain("*", csp.Replace("'self'", ""));
        Assert.Equal(new[] { "nosniff" }, Values(response, "X-Content-Type-Options"));
        Assert.Equal(new[] { "no-referrer" }, Values(response, "Referrer-Policy"));
    }

    [Fact]
    public async Task ApiReference_InDevelopment_IsTheOnlyDocumentedCspException()
    {
        using var client = factory.CreateClient();
        using var scalar = await client.GetAsync("/scalar/v1");
        Assert.Empty(Values(scalar, "Content-Security-Policy")); // виняток лише для Development-довідника
        Assert.Equal(new[] { "nosniff" }, Values(scalar, "X-Content-Type-Options"));
    }

    // ------------------------------------------------------------ клієнт без небезпечних sink-ів
    [Theory]
    [InlineData("/browser-lab.js")]
    [InlineData("/app.js")]
    [InlineData("/auth-lab.js")]
    public async Task ClientScripts_DoNotUseDangerousHtmlOrCodeSinks(string path)
    {
        using var client = factory.CreateClient();
        var script = await client.GetStringAsync(path);
        foreach (var sink in new[] { "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "eval(", "new Function" })
            Assert.DoesNotContain(sink, script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowserLabHtml_HasNoInlineScriptsOrEventHandlers()
    {
        using var client = factory.CreateClient();
        foreach (var page in new[] { "/browser-lab.html", "/auth-lab.html", "/" })
        {
            var html = await client.GetStringAsync(page);
            Assert.DoesNotContain("onerror=", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onclick=", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase); // CSS лише зовнішні файли
        }
    }

    // ------------------------------------------------------------ збережене значення не змінюється
    [Fact]
    public async Task SeededMarkerComment_IsStoredAsPlainText_ReturnedUnchangedByApi()
    {
        using var alice = await factory.LoginAsync("alice", TestPassword.Seed);
        var json = await alice.GetStringAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        var comments = JsonDocument.Parse(json).RootElement.GetProperty("comments").EnumerateArray()
            .Select(c => c.GetProperty("text").GetString()).ToArray();
        Assert.Contains(Marker, comments); // API не відфільтровує і не змінює вхід: виправлення лише в sink
    }

    [Fact]
    public async Task Description_WithHtmlLikeMarker_IsCreatedAndReturnedByteForByte()
    {
        using var alice = await factory.LoginAsync("alice", TestPassword.Seed);
        var title = $"LR5-{Guid.NewGuid():N}";
        using var created = await alice.PostAsJsonAsync("/api/incidents", new
        {
            title, description = Marker, severity = "Low", occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        try
        {
            var details = await alice.GetStringAsync($"/api/incidents/{id}");
            Assert.Equal(Marker, JsonDocument.Parse(details).RootElement.GetProperty("description").GetString());
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(
                db.Incidents.Where(item => item.Id == id));
        }
    }
}
