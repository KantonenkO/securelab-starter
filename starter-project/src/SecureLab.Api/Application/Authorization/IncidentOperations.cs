using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace SecureLab.Api.Application.Authorization;

/// <summary>Операції над Incident, для яких сервер ухвалює рішення (рядки матриці docs/access-matrix.md).</summary>
public static class IncidentOperations
{
    public static readonly OperationAuthorizationRequirement Read = new() { Name = nameof(Read) };
    public static readonly OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };
    public static readonly OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
    public static readonly OperationAuthorizationRequirement Triage = new() { Name = nameof(Triage) };
    public static readonly OperationAuthorizationRequirement ReadComment = new() { Name = nameof(ReadComment) };
}
