using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

// VULNERABLE STATE (ЛР 04): ресурс шукається лише за id, без порівняння з principal і без рішення про право.
// Вимагається лише автентифікація (RequireAuthorization), тож будь-який користувач діє над будь-яким id.
public static class IncidentMutationEndpoints
{
    public static IEndpointRouteBuilder MapIncidentMutationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/incidents/{id:guid}", async (Guid id, UpdateIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            var incident = await db.Incidents.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (incident is null) return Results.Problem(title: "Інцидент не знайдено", statusCode: 404);
            if (request.Title is not null) incident.Title = request.Title;
            if (request.Description is not null) incident.Description = request.Description;
            if (Enum.TryParse<IncidentSeverity>(request.Severity, true, out var severity)) incident.Severity = severity;
            if (Enum.TryParse<IncidentStatus>(request.Status, true, out var status)) incident.Status = status;
            if (request.OwnerUserId is { } owner) incident.OwnerUserId = owner;
            incident.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new IncidentChangedResponse(incident.Id, incident.Title, incident.Severity.ToString(), incident.Status.ToString(), incident.UpdatedAtUtc));
        }).RequireAuthorization();

        endpoints.MapDelete("/api/incidents/{id:guid}", async (Guid id, SecureLabDbContext db, CancellationToken ct) =>
        {
            var incident = await db.Incidents.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (incident is null) return Results.Problem(title: "Інцидент не знайдено", statusCode: 404);
            db.Incidents.Remove(incident);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        endpoints.MapPost("/api/incidents/{id:guid}/triage", async (Guid id, TriageIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            var incident = await db.Incidents.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (incident is null) return Results.Problem(title: "Інцидент не знайдено", statusCode: 404);
            // Статус бере з тіла клієнта: роль і поточний стан не перевіряються.
            if (Enum.TryParse<IncidentStatus>(request.Status, true, out var next)) incident.Status = next;
            else incident.Status = IncidentStatus.Triaged;
            incident.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new IncidentChangedResponse(incident.Id, incident.Title, incident.Severity.ToString(), incident.Status.ToString(), incident.UpdatedAtUtc));
        }).RequireAuthorization();

        endpoints.MapGet("/api/incident-comments/{id:guid}", async (Guid id, SecureLabDbContext db, CancellationToken ct) =>
        {
            // Child шукається лише за своїм id; internal-коментарі не фільтруються.
            var comment = await db.IncidentComments.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new IncidentCommentDetailsResponse(item.Id, item.IncidentId, item.Author.DisplayName, item.Text, item.IsInternal, item.CreatedAtUtc))
                .SingleOrDefaultAsync(ct);
            return comment is null ? Results.Problem(title: "Коментар не знайдено", statusCode: 404) : Results.Ok(comment);
        }).RequireAuthorization();

        return endpoints;
    }
}
