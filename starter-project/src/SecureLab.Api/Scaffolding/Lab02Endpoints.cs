using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Authorization;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    private const int TitleMaxLength = 160;
    private const int DescriptionMaxLength = 4000;
    private const int HighSeverityMinDescription = 40;
    private const int OccurredAtMaxAgeDays = 365;

    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, HttpContext httpContext, SecureLabDbContext db, CancellationToken ct) =>
        {
            // Значення q лише екранується як literal substring і йде в запит ПАРАМЕТРОМ (@pattern).
            var pattern = "%" + EscapeLike(q ?? "") + "%";
            // ЛР 04 (A-11): умова видимості за principal стоїть у самому запиті - до сортування й Take(50).
            var visible = IncidentAccessRules.VisibleTo(
                db.Incidents.AsNoTracking(), IncidentAccessRules.UserId(httpContext.User), IncidentAccessRules.IsAnalyst(httpContext.User));
            var found = visible
                .Where(item => EF.Functions.ILike(item.Title, pattern, "\\")
                    || EF.Functions.ILike(item.Description, pattern, "\\"));

            // sortBy - структура запиту: allowlist вибирає один із написаних тут виразів.
            IQueryable<Incident>? ordered = sortBy switch
            {
                null or "" or "createdAtUtc" => found
                    .OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id),
                "severity" => found
                    .OrderBy(item => item.Severity == IncidentSeverity.Critical ? 0
                        : item.Severity == IncidentSeverity.High ? 1
                        : item.Severity == IncidentSeverity.Medium ? 2 : 3)
                    .ThenBy(item => item.Id),
                "status" => found
                    .OrderBy(item => item.Status == IncidentStatus.New ? 0
                        : item.Status == IncidentStatus.Triaged ? 1
                        : item.Status == IncidentStatus.InProgress ? 2
                        : item.Status == IncidentStatus.Resolved ? 3 : 4)
                    .ThenBy(item => item.Id),
                _ => null
            };
            if (ordered is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["sortBy"] = ["Допустимі значення: createdAtUtc, severity, status."]
                });

            var rows = await ordered
                .Take(50)
                .Select(item => new SearchIncidentResponse(
                    item.Id, item.Title, item.Description,
                    item.Severity.ToString(), item.Status.ToString(), item.CreatedAtUtc))
                .ToListAsync(ct);
            return Results.Ok(rows);
        }).RequireAuthorization();
        app.MapPost("/api/incidents", async (CreateIncidentRequest request, HttpContext httpContext, SecureLabDbContext db, CancellationToken ct) =>
        {
            // ЛР 03: власника встановлює сервер із перевіреного principal (cookie), а не з тіла чи header.
            if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerUserId))
                return Results.Unauthorized();
            // Час фіксуємо один раз на початку обробки.
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();

            // 1. Базові перевірки: required та довжина (довжина вимірюється до Trim()).
            if (string.IsNullOrWhiteSpace(request.Title))
                errors["title"] = ["Заголовок обов'язковий."];
            else if (request.Title.Length > TitleMaxLength)
                errors["title"] = [$"Заголовок не довший за {TitleMaxLength} символів."];

            if (string.IsNullOrWhiteSpace(request.Description))
                errors["description"] = ["Опис обов'язковий."];
            else if (request.Description.Length > DescriptionMaxLength)
                errors["description"] = [$"Опис не довший за {DescriptionMaxLength} символів."];

            // 2. Enum: і перетворення, і належність до оголошених значень ("7" та "Low,High" відхиляються).
            var severity = IncidentSeverity.Low;
            var severityIsValid =
                request.Severity is not null
                && !request.Severity.Contains(',')
                && Enum.TryParse(request.Severity, ignoreCase: true, out severity)
                && Enum.IsDefined(severity);
            if (!severityIsValid)
                errors["severity"] = ["Допустимі значення: Low, Medium, High, Critical."];

            // 3. Дата: обов'язкова, не пізніше now + 5 хв; T-10: не старіша за 365 днів.
            if (request.OccurredAtUtc is null)
                errors["occurredAtUtc"] = ["Час події обов'язковий."];
            else if (request.OccurredAtUtc.Value > now.AddMinutes(5))
                errors["occurredAtUtc"] = ["Час події не може бути більш ніж на 5 хвилин у майбутньому."];
            else if (request.OccurredAtUtc.Value < now.AddDays(-OccurredAtMaxAgeDays))
                errors["occurredAtUtc"] = [$"Час події не може бути старшим за {OccurredAtMaxAgeDays} днів."];

            // 4. Нормалізація по одному разу.
            var title = request.Title?.Trim() ?? "";
            var description = request.Description?.Trim() ?? "";

            // 5. Cross-field (T-09): High/Critical потребують опису від 40 символів.
            if (severityIsValid
                && severity is IncidentSeverity.High or IncidentSeverity.Critical
                && !errors.ContainsKey("description")
                && description.Length < HighSeverityMinDescription)
                errors["description"] = [$"Для High або Critical опис має містити щонайменше {HighSeverityMinDescription} символів."];

            // 6. Усі помилки контракту - одна відповідь 400 до звернення до БД.
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // 7. Предметний конфлікт (T-03): активний інцидент із тим самим title (з урахуванням регістру).
            var duplicate = await db.Incidents.AnyAsync(
                item => item.Title == title && item.Status != IncidentStatus.Closed, ct);
            if (duplicate)
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Інцидент уже існує",
                    detail: "Активний інцидент із таким заголовком уже зареєстровано.");

            // 8. Entity: server-managed поля встановлює лише сервер.
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Title = title,
                Description = description,
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Incidents.Add(incident);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Конфлікт даних",
                    detail: "Запис суперечить наявним даним.");
            }

            // 9. Response DTO: лише поля зовнішнього контракту.
            var response = new CreatedIncidentResponse(
                incident.Id, incident.Title, incident.Severity.ToString(), incident.Status.ToString(),
                incident.OccurredAtUtc, incident.CreatedAtUtc, incident.UpdatedAtUtc);
            return Results.Created($"/api/incidents/{incident.Id}", response);
        }).RequireAuthorization();
    }

    // Спочатку екрануємо сам escape-символ, потім _ і %; зовнішні % додає виклик.
    public static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("_", "\\_").Replace("%", "\\%");
}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);

public sealed record SearchIncidentResponse(
    Guid Id, string Title, string Description, string Severity, string Status, DateTimeOffset CreatedAtUtc);

public sealed record CreatedIncidentResponse(
    Guid Id, string Title, string Severity, string Status,
    DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
