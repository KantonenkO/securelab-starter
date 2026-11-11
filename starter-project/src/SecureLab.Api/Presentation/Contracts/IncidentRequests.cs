using System.Text.Json.Serialization;

namespace SecureLab.Api.Presentation.Contracts;

// Звичайний update: лише content fields. Невідомі та заборонені поля (owner, status, severity, role ...) відхиляються
// контрактом (A-08b): 400, стан не змінюється. Owner/status/id завжди визначає сервер.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateIncidentRequest(string? Title, string? Description);

// Transition: запит не може надати status, role чи owner; лише необов'язкове обґрунтування.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TriageIncidentRequest(string? Reason);

public sealed record IncidentCommentDetailsResponse(
    Guid Id, Guid IncidentId, string AuthorDisplayName, string Text, bool IsInternal, DateTimeOffset CreatedAtUtc);

public sealed record IncidentChangedResponse(Guid Id, string Title, string Severity, string Status, DateTimeOffset UpdatedAtUtc);
