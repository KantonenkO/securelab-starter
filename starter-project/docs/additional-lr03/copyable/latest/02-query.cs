public Task<LatestIncidentResponse?> GetLatestAsync(
    CancellationToken cancellationToken)
{
    return dbContext.Incidents
        .AsNoTracking()
        .OrderByDescending(incident => incident.CreatedAtUtc)
        .ThenBy(incident => incident.Id)
        .Select(incident => new LatestIncidentResponse(
            incident.Title,
            incident.CreatedAtUtc))
        .FirstOrDefaultAsync(cancellationToken);
}
