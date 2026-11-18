using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Security;

namespace SecureLab.Api.Tests;

// ЛР 6, REG-CSRF-PERIMETER: периметр не тримається на пам'яті. Перелік змінювальних маршрутів береться з реєстру
// endpoint-ів застосунку; кожен POST/PUT/PATCH/DELETE мусить мати antiforgery-валідацію АБО явне виключення з причиною.
public sealed class CsrfPerimeterTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static readonly string[] Unsafe = ["POST", "PUT", "PATCH", "DELETE"];
    private static readonly string[] ExpectedExempt = ["POST /api/auth/login", "POST /api/auth/register"];

    private IEnumerable<(string Key, RouteEndpoint Endpoint)> MutatingEndpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Where(method => Unsafe.Contains(method, StringComparer.OrdinalIgnoreCase))
                .Select(method => (method.ToUpperInvariant() + " " + endpoint.RoutePattern.RawText, endpoint)));

    [Fact]
    public void REG_CSRF_PERIMETER_EveryMutatingEndpoint_IsProtected_OrExplicitlyExempt()
    {
        var all = MutatingEndpoints().ToList();
        Assert.NotEmpty(all);
        var unprotected = all.Where(item => item.Endpoint.Metadata.GetMetadata<AntiforgeryRequiredMetadata>() is null
            && item.Endpoint.Metadata.GetMetadata<AntiforgeryExemptMetadata>() is null).Select(item => item.Key).ToList();
        Assert.True(unprotected.Count == 0, "Змінювальні маршрути без antiforgery/виключення: " + string.Join(", ", unprotected));
    }

    [Fact]
    public void REG_CSRF_PERIMETER_Exemptions_AreOnlyPreAuthEndpoints_WithReason()
    {
        var exempt = MutatingEndpoints()
            .Where(item => item.Endpoint.Metadata.GetMetadata<AntiforgeryExemptMetadata>() is not null).ToList();
        Assert.Equal(ExpectedExempt.OrderBy(x => x), exempt.Select(item => item.Key).OrderBy(x => x));
        Assert.All(exempt, item => Assert.False(string.IsNullOrWhiteSpace(
            item.Endpoint.Metadata.GetMetadata<AntiforgeryExemptMetadata>()!.Reason)));
        // Виключення не стосуються cookie-authenticated маршрутів: жоден не вимагає авторизації.
        Assert.All(exempt, item => Assert.Empty(item.Endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public void REG_CSRF_PERIMETER_CookieAuthenticatedMutations_Inventory_IsExactly_SC01_to_SC05()
    {
        var protectedKeys = MutatingEndpoints()
            .Where(item => item.Endpoint.Metadata.GetMetadata<AntiforgeryRequiredMetadata>() is not null)
            .Select(item => item.Key).OrderBy(x => x).ToArray();
        string[] expected =
        [
            "DELETE /api/incidents/{id:guid}", "POST /api/auth/logout", "POST /api/incidents",
            "POST /api/incidents/{id:guid}/triage", "PUT /api/incidents/{id:guid}"
        ];
        Assert.Equal(expected.OrderBy(x => x), protectedKeys);
    }
}
