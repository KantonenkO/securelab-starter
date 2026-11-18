using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SecureLab.Api.Security;

public sealed class WindowLimit
{
    public int PermitLimit { get; set; } = 30;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class AbuseLimitOptions
{
    // Пошук: квота на перевірений principal (user id) за вікно.
    public WindowLimit Search { get; set; } = new() { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) };
    // Реєстрація: публічний endpoint, ключ - IP; квота невелика (account-creation flooding / enumeration).
    public WindowLimit Register { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };
}

/// <summary>
/// ЛР 6: rate limiting як захист від зловживання (не заміна authentication/authorization). Два іменованих policy,
/// квота/вікно з конфігурації <c>RateLimiting</c>. Middleware стоїть ПІСЛЯ UseAuthorization: анонімний запит до
/// protected route дає 401, а не 429, і не споживає квоту; 429 не розкриває існування акаунтів чи даних.
/// </summary>
public static class AbuseLimits
{
    public const string SearchPolicy = "search";
    public const string RegisterPolicy = "register";

    public static void AddAbuseLimits(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<AbuseLimitOptions>(builder.Configuration.GetSection("RateLimiting"));
        builder.Services.AddRateLimiter(_ => { });
        // Політики створюються лише після побудови DI: налаштування (у т.ч. test overrides) читаються з IOptions.
        builder.Services.AddOptions<RateLimiterOptions>().Configure<IOptions<AbuseLimitOptions>>((options, abuse) =>
        {
            var settings = abuse.Value;
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(SearchPolicy, context =>
            {
                // Partition key = перевірений principal; для невпізнаного запиту - IP (до цього місця він не дійде: 401).
                var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var key = user is not null ? "user:" + user : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                return Fixed(key, settings.Search);
            });
            options.AddPolicy(RegisterPolicy, context =>
                Fixed("ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"), settings.Register));
            options.OnRejected = RejectAsync;
        });
    }

    private static RateLimitPartition<string> Fixed(string key, WindowLimit limit) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit.PermitLimit, Window = limit.Window, QueueLimit = 0, AutoReplenishment = true
        });

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds) : 60;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = seconds.ToString();
        // Однакова форма відповіді для будь-якого тіла запиту; без деталей про ключ, акаунт чи дані.
        await http.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests",
            Detail = "Забагато запитів. Повторіть пізніше.",
            Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? http.TraceIdentifier }
        }, options: null, contentType: "application/problem+json", cancellationToken);
    }
}
