using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Application.Authorization;

/// <summary>Resource-based handler для Incident: рішення залежить від суб'єкта, операції та стану завантаженого ресурсу.</summary>
public sealed class IncidentAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, Incident>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OperationAuthorizationRequirement requirement, Incident incident)
    {
        var userId = IncidentAccessRules.UserId(context.User);
        var analyst = IncidentAccessRules.IsAnalyst(context.User);
        var allowed = requirement.Name switch
        {
            nameof(IncidentOperations.Read) => IncidentAccessRules.CanRead(userId, analyst, incident.OwnerUserId),
            nameof(IncidentOperations.Update) => IncidentAccessRules.CanUpdate(userId, incident.OwnerUserId),
            nameof(IncidentOperations.Delete) => IncidentAccessRules.CanDelete(userId, incident.OwnerUserId, incident.Status),
            nameof(IncidentOperations.Triage) => IncidentAccessRules.CanTriage(analyst),
            _ => false // невідома операція: deny за замовчуванням
        };
        if (allowed) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Handler для child-ресурсу: рішення приймається через parent incident і прапорець IsInternal.</summary>
public sealed class IncidentCommentAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, IncidentComment>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OperationAuthorizationRequirement requirement, IncidentComment comment)
    {
        if (requirement.Name == nameof(IncidentOperations.ReadComment)
            && IncidentAccessRules.CanReadComment(
                IncidentAccessRules.UserId(context.User), IncidentAccessRules.IsAnalyst(context.User),
                comment.Incident.OwnerUserId, comment.IsInternal))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
