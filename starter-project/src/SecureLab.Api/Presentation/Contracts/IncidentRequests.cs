namespace SecureLab.Api.Presentation.Contracts;

// VULNERABLE STATE (ЛР 04): звичайний update DTO містить поля, якими має керувати сервер.
public sealed record UpdateIncidentRequest(
    string? Title, string? Description, string? Severity, string? Status, Guid? OwnerUserId);

public sealed record TriageIncidentRequest(string? Status, string? Reason);

public sealed record IncidentCommentDetailsResponse(
    Guid Id, Guid IncidentId, string AuthorDisplayName, string Text, bool IsInternal, DateTimeOffset CreatedAtUtc);

public sealed record IncidentChangedResponse(Guid Id, string Title, string Severity, string Status, DateTimeOffset UpdatedAtUtc);
