using Microsoft.AspNetCore.Authorization;
using SecureLab.Api.Application.Authorization;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // ЛР 04: уся група вимагає автентифікації (401 для анонімного запиту); рішення для ресурсу - нижче.
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents")
            .RequireAuthorization();

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>();

        return endpoints;
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await queries.GetSeveritySummaryAsync(
            IncidentAccessRules.UserId(httpContext.User), IncidentAccessRules.IsAnalyst(httpContext.User), cancellationToken));
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        return Results.Ok(await queries.GetListAsync(
            parsedStatus, IncidentAccessRules.UserId(httpContext.User), IncidentAccessRules.IsAnalyst(httpContext.User),
            cancellationToken));
    }

    // Головна protected action (ЛР 04): owner - allow; інший Reporter - 404 (як для неіснуючого); Analyst - allow для аналізу.
    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        HttpContext httpContext,
        IAuthorizationService authorization,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var incident = await queries.FindForDecisionAsync(id, cancellationToken);
        if (incident is null)
        {
            return IncidentProblems.NotFound();
        }

        var decision = await authorization.AuthorizeAsync(httpContext.User, incident, IncidentOperations.Read);
        if (!decision.Succeeded)
        {
            return IncidentProblems.NotFound(); // чужий ресурс не відрізняється від неіснуючого
        }

        var details = await queries.GetDetailsAsync(
            id, includeInternalComments: IncidentAccessRules.IsAnalyst(httpContext.User), cancellationToken);
        return details is null ? IncidentProblems.NotFound() : Results.Ok(details);
    }
}
