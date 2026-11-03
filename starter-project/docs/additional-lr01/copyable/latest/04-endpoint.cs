private static async Task<IResult> GetLatestAsync(
    IncidentQueries queries,
    CancellationToken cancellationToken)
{
    var latest = await queries.GetLatestAsync(cancellationToken);
    return latest is null
        ? Results.Problem(
            title: "Інцидентів ще немає",
            statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(latest);
}
