using System.Security.Claims;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Application.Authorization;

/// <summary>
/// Єдине місце правил доступу до Incident (baseline 2-A). Identifier і роль беруться лише з verified principal;
/// handler-и та query scoping викликають ті самі функції, тож правило не розходиться між шляхами.
/// </summary>
public static class IncidentAccessRules
{
    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool IsAnalyst(ClaimsPrincipal user) => user.IsInRole(DbSeeder.AnalystRole);

    // Читання: власник або Analyst. Administrator прав «на все» не має (в предметному правилі їх немає).
    public static bool CanRead(Guid? userId, bool isAnalyst, Guid ownerUserId) =>
        isAnalyst || (userId is not null && userId == ownerUserId);

    // Зміна вмісту: лише власник; Analyst читає, але не змінює вміст.
    public static bool CanUpdate(Guid? userId, Guid ownerUserId) => userId is not null && userId == ownerUserId;

    // Видалення: власник і лише поки інцидент у статусі New.
    public static bool CanDelete(Guid? userId, Guid ownerUserId, IncidentStatus status) =>
        CanUpdate(userId, ownerUserId) && status == IncidentStatus.New;

    // Transition (triage): лише Analyst. Допустимість поточного статусу (New) перевіряє endpoint (409).
    public static bool CanTriage(bool isAnalyst) => isAnalyst;

    // Child: доступ до parent + internal лише для Analyst.
    public static bool CanReadComment(Guid? userId, bool isAnalyst, Guid parentOwnerUserId, bool isInternal) =>
        CanRead(userId, isAnalyst, parentOwnerUserId) && (!isInternal || isAnalyst);

    // Query scoping: та сама умова видимості всередині запиту (list, search, severity-summary).
    public static IQueryable<Incident> VisibleTo(IQueryable<Incident> incidents, Guid? userId, bool isAnalyst) =>
        isAnalyst ? incidents : incidents.Where(incident => userId != null && incident.OwnerUserId == userId);
}
