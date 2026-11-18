using Microsoft.AspNetCore.Antiforgery;

namespace SecureLab.Api.Security;

/// <summary>Маркер: маршрут проходить antiforgery-валідацію (читається regression-тестом REG-CSRF-PERIMETER).</summary>
public sealed class AntiforgeryRequiredMetadata;

/// <summary>Маркер явного виключення з обов'язковою причиною (login/register не покладаються на authentication cookie).</summary>
public sealed record AntiforgeryExemptMetadata(string Reason);

/// <summary>
/// ЛР 6 (F-01). Стандартний ASP.NET Core antiforgery (IAntiforgery): cookie-частина + request token у header.
/// Єдиний спосіб валідації - endpoint filter <see cref="AntiforgeryFilter"/>, який перетворює
/// AntiforgeryValidationException на один точний 400 Problem Details (а не 500) і не викликає handler.
/// UseAntiforgery() middleware тут недостатній: він перевіряє лише endpoint-и з form data, а не JSON/DELETE.
/// </summary>
public static class AntiforgeryProtection
{
    public const string HeaderName = "X-CSRF-TOKEN";
    public const string TokenRoute = "/api/antiforgery/token";
    public const string RejectionTitle = "Antiforgery token is missing or invalid.";

    public static void AddAntiforgeryProtection(this WebApplicationBuilder builder)
    {
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = "SecureLab.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            // Як і cookie сеансу: Development допускає HTTP, інші профілі - лише Secure.
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
    }

    /// <summary>Same-origin маршрут видачі request token; cookie-частину framework ставить сам (Set-Cookie).</summary>
    public static void MapAntiforgeryEndpoints(this WebApplication app)
    {
        app.MapGet(TokenRoute, (HttpContext http, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(http);
                http.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new { token = tokens.RequestToken });
            })
            .AllowAnonymous() // bootstrap до login; токен прив'язується до поточного principal (після login запитують новий)
            .WithName("GetAntiforgeryToken")
            .ExcludeFromDescription();
    }

    /// <summary>Підключає валідацію до cookie-authenticated змінювального маршруту.</summary>
    public static TBuilder RequireAntiforgery<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AntiforgeryRequiredMetadata());
        // Фабрика фільтра працює для будь-якого IEndpointConventionBuilder (route handler і group); IAntiforgery беремо з DI запиту.
        builder.AddEndpointFilterFactory((_, next) => invocation =>
            new AntiforgeryFilter(invocation.HttpContext.RequestServices.GetRequiredService<IAntiforgery>())
                .InvokeAsync(invocation, next));
        return builder;
    }

    public static TBuilder ExemptFromAntiforgery<TBuilder>(this TBuilder builder, string reason) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new AntiforgeryExemptMetadata(reason));
}

public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            // Тіло не містить значень токена чи деталей exception; traceId додає спільний Problem Details.
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: AntiforgeryProtection.RejectionTitle);
        }
        return await next(context);
    }
}
