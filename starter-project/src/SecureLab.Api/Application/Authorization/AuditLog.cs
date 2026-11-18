using System.Diagnostics;
using System.Security.Claims;

namespace SecureLab.Api.Application.Authorization;

/// <summary>
/// Безпечна аудиторська подія про відмовлену привілейовану дію. Поля: server-verified subject id, action,
/// resource reference, result, серверний час і той самий trace id, що у Problem Details відповіді.
/// У подію не потрапляють cookie, пароль, token, тіло запиту, stack trace.
/// </summary>
public static class AuditLog
{
    public static void Denied(ILoggerFactory loggers, HttpContext http, string action, Guid resourceId)
    {
        // Те саме значення, яке ASP.NET Core пише в поле traceId Problem Details.
        var traceId = Activity.Current?.Id ?? http.TraceIdentifier;
        loggers.CreateLogger("SecureLab.Audit").LogWarning(
            "authorization.denied subject={SubjectId} action={Action} resource={Resource} result={Result} occurredAtUtc={OccurredAtUtc} traceId={TraceId}",
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", action, $"incident:{resourceId}", "denied",
            DateTimeOffset.UtcNow.ToString("O"), traceId);
    }

    /// <summary>
    /// F-05: дозволений привілейований перехід статусу. Ті самі поля, що й у відмові (subject, action, resource, result,
    /// time, traceId) + пара статусів. Free-text причина, cookie, токени й тіло запиту в подію не потрапляють.
    /// </summary>
    public static void StatusChanged(ILoggerFactory loggers, HttpContext http, string action, Guid resourceId, string from, string to)
    {
        var traceId = Activity.Current?.Id ?? http.TraceIdentifier;
        loggers.CreateLogger("SecureLab.Audit").LogInformation(
            "incident.status.changed subject={SubjectId} action={Action} resource={Resource} result={Result} from={From} to={To} occurredAtUtc={OccurredAtUtc} traceId={TraceId}",
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", action, $"incident:{resourceId}", "allowed",
            from, to, DateTimeOffset.UtcNow.ToString("O"), traceId);
    }
}
