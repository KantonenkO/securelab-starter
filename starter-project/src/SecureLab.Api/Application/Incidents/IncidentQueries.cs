using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Authorization;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

public sealed class IncidentQueries(SecureLabDbContext dbContext, ILogger<IncidentQueries> logger)
{
    // ЛР 04: userId і роль приходять з verified principal. Умова видимості входить у запит (query scoping),
    // тож заборонені рядки не завантажуються з БД.
    public async Task<IReadOnlyList<IncidentListItemResponse>> GetListAsync(
        IncidentStatus? status,
        Guid? userId,
        bool isAnalyst,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incidents with status filter {Status}", status);

        var query = IncidentAccessRules.VisibleTo(dbContext.Incidents.AsNoTracking(), userId, isAnalyst);
        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }

        return await query
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .Select(incident => new IncidentListItemResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    // Завантаження для рішення handler-а (без проєкції); AsNoTracking: тут лише читання.
    public Task<Incident?> FindForDecisionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Incidents.AsNoTracking().SingleOrDefaultAsync(incident => incident.Id == id, cancellationToken);

    // Details-проєкція викликається ЛИШЕ після позитивного рішення. Internal-коментарі бачить тільки Analyst.
    public Task<IncidentDetailsResponse?> GetDetailsAsync(Guid id, bool includeInternalComments, CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incident {IncidentId}", id);

        return dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => new IncidentDetailsResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.Owner.DisplayName,
                incident.Comments
                    .Where(comment => includeInternalComments || !comment.IsInternal)
                    .OrderBy(comment => comment.CreatedAtUtc)
                    .Select(comment => new IncidentCommentResponse(
                        comment.Id,
                        comment.Author.DisplayName,
                        comment.Text,
                        comment.CreatedAtUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncidentSeveritySummaryResponse>> GetSeveritySummaryAsync(
        Guid? userId,
        bool isAnalyst,
        CancellationToken cancellationToken)
    {
        // Обмеження видимості - до групування: чужі записи не потрапляють в агрегат.
        var counts = await IncidentAccessRules.VisibleTo(dbContext.Incidents.AsNoTracking(), userId, isAnalyst)
            .GroupBy(incident => incident.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        logger.LogInformation("Severity summary computed for {GroupCount} groups", counts.Count);

        return Enum.GetValues<IncidentSeverity>()
            .Select(severity => new IncidentSeveritySummaryResponse(
                severity.ToString(),
                counts.FirstOrDefault(c => c.Severity == severity)?.Count ?? 0))
            .ToList();
    }
}
