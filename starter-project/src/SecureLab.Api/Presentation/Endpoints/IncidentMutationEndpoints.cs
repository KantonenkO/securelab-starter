using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Authorization;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using SecureLab.Api.Security;

namespace SecureLab.Api.Presentation.Endpoints;

// FIXED STATE (ЛР 04): ресурс завантажується (для mutation - tracked), рішення ухвалює resource-based handler
// ДО будь-якої зміни стану; DTO не містить owner/status/role. Політика відмов - docs/access-matrix.md.
public static class IncidentMutationEndpoints
{
    private const int TitleMax = 160, DescriptionMax = 4000, ReasonMax = 500;

    public static IEndpointRouteBuilder MapIncidentMutationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/incidents/{id:guid}", UpdateAsync).RequireAuthorization().RequireAntiforgery();
        endpoints.MapDelete("/api/incidents/{id:guid}", DeleteAsync).RequireAuthorization().RequireAntiforgery();
        endpoints.MapPost("/api/incidents/{id:guid}/triage", TriageAsync).RequireAuthorization().RequireAntiforgery();
        endpoints.MapGet("/api/incident-comments/{id:guid}", GetCommentAsync).RequireAuthorization();
        return endpoints;
    }

    // Чужий (невидимий) ресурс -> 404; видимий, але дія заборонена -> 403.
    private static async Task<IResult> DenyAsync(HttpContext http, IAuthorizationService authorization, Incident incident)
    {
        var canRead = await authorization.AuthorizeAsync(http.User, incident, IncidentOperations.Read);
        return canRead.Succeeded ? IncidentProblems.Forbidden() : IncidentProblems.NotFound();
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateIncidentRequest request, HttpContext http, IAuthorizationService authorization,
        SecureLabDbContext db, CancellationToken ct)
    {
        var incident = await db.Incidents.SingleOrDefaultAsync(item => item.Id == id, ct); // tracked
        if (incident is null) return IncidentProblems.NotFound();
        var decision = await authorization.AuthorizeAsync(http.User, incident, IncidentOperations.Update);
        if (!decision.Succeeded) return await DenyAsync(http, authorization, incident);

        var errors = new Dictionary<string, string[]>();
        if (request.Title is null && request.Description is null)
            errors["body"] = ["Потрібно вказати title або description."];
        if (request.Title is not null && (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > TitleMax))
            errors["title"] = [$"Заголовок обов'язковий і не довший за {TitleMax} символів."];
        if (request.Description is not null && (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > DescriptionMax))
            errors["description"] = [$"Опис обов'язковий і не довший за {DescriptionMax} символів."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        if (request.Title is not null) incident.Title = request.Title.Trim();
        if (request.Description is not null) incident.Description = request.Description.Trim();
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(Changed(incident));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, HttpContext http, IAuthorizationService authorization, SecureLabDbContext db, CancellationToken ct)
    {
        var incident = await db.Incidents.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (incident is null) return IncidentProblems.NotFound();
        var decision = await authorization.AuthorizeAsync(http.User, incident, IncidentOperations.Delete);
        if (!decision.Succeeded) return await DenyAsync(http, authorization, incident);
        db.Incidents.Remove(incident);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // Порядок: tracked entity -> рішення за роллю -> (deny: audit і відмова) -> перевірка поточного статусу ->
    // сервер сам визначає наступний статус -> SaveChanges.
    private static async Task<IResult> TriageAsync(
        Guid id, TriageIncidentRequest? request, HttpContext http, IAuthorizationService authorization,
        SecureLabDbContext db, ILoggerFactory loggers, CancellationToken ct)
    {
        var incident = await db.Incidents.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (incident is null) return IncidentProblems.NotFound();
        var decision = await authorization.AuthorizeAsync(http.User, incident, IncidentOperations.Triage);
        if (!decision.Succeeded)
        {
            AuditLog.Denied(loggers, http, "incident.triage", incident.Id);
            return await DenyAsync(http, authorization, incident);
        }
        if (request?.Reason is { Length: > ReasonMax })
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = [$"Не довше {ReasonMax} символів."] });
        if (incident.Status != IncidentStatus.New) return IncidentProblems.InvalidTransition();

        var userId = IncidentAccessRules.UserId(http.User)!.Value;
        var now = DateTimeOffset.UtcNow;
        db.IncidentStatusHistory.Add(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(), IncidentId = incident.Id, ChangedByUserId = userId,
            OldStatus = incident.Status, NewStatus = IncidentStatus.Triaged,
            Note = string.IsNullOrWhiteSpace(request?.Reason) ? "Triage" : request!.Reason!.Trim(), CreatedAtUtc = now
        });
        incident.Status = IncidentStatus.Triaged; // next status визначає сервер
        incident.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return Results.Ok(Changed(incident));
    }

    // Child через parent: коментар завантажується разом із parent incident, рішення - той самий централізований механізм.
    private static async Task<IResult> GetCommentAsync(
        Guid id, HttpContext http, IAuthorizationService authorization, SecureLabDbContext db, CancellationToken ct)
    {
        var comment = await db.IncidentComments.AsNoTracking().Include(item => item.Incident).Include(item => item.Author)
            .SingleOrDefaultAsync(item => item.Id == id, ct);
        if (comment is null) return IncidentProblems.CommentNotFound();
        var decision = await authorization.AuthorizeAsync(http.User, comment, IncidentOperations.ReadComment);
        if (!decision.Succeeded) return IncidentProblems.CommentNotFound();
        return Results.Ok(new IncidentCommentDetailsResponse(
            comment.Id, comment.IncidentId, comment.Author.DisplayName, comment.Text, comment.IsInternal, comment.CreatedAtUtc));
    }

    private static IncidentChangedResponse Changed(Incident incident) =>
        new(incident.Id, incident.Title, incident.Severity.ToString(), incident.Status.ToString(), incident.UpdatedAtUtc);
}
