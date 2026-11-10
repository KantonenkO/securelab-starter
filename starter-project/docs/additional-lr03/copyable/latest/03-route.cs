group.MapGet("/latest", GetLatestAsync)
    .WithName("GetLatestIncident")
    .Produces<LatestIncidentResponse>()
    .ProducesProblem(StatusCodes.Status404NotFound);
