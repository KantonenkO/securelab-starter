using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

// ЛР 02: T-01, T-02, T-03, T-09, T-10, A-02 для POST /api/incidents.
public sealed class CreateContractTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object Body(string title, string? description = null, string severity = "Low",
        DateTimeOffset? occurredAt = null) => new
    {
        title,
        description = description ?? "Навчальний опис коректного інциденту для перевірки контракту.",
        severity,
        occurredAtUtc = occurredAt ?? DateTimeOffset.UtcNow.AddMinutes(-10)
    };

    private async Task CleanupAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        await db.Incidents.Where(item => item.Title == title).ExecuteDeleteAsync();
    }

    private static void AssertSafeProblem(string json)
    {
        foreach (var forbidden in new[] { "Exception", "StackTrace", "Npgsql", "SELECT", "Host=", "Password" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonElement> AssertValidationAsync(HttpResponseMessage response, string key)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        AssertSafeProblem(json);
        var errors = JsonDocument.Parse(json).RootElement.GetProperty("errors").Clone();
        Assert.True(errors.TryGetProperty(key, out _), $"errors має містити ключ '{key}'");
        return errors;
    }

    [Fact]
    public async Task T01_ValidRequest_Returns201WithResponseDto()
    {
        var title = $"T01-{Guid.NewGuid():N}";
        try
        {
            using var response = await _client.PostAsJsonAsync("/api/incidents", Body("  " + title + "  ",
                occurredAt: new DateTimeOffset(DateTime.UtcNow.AddHours(-1), TimeSpan.Zero)));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(title, root.GetProperty("title").GetString());
            Assert.Equal("New", root.GetProperty("status").GetString());
            Assert.False(root.TryGetProperty("ownerUserId", out _));
            Assert.False(root.TryGetProperty("description", out _));
        }
        finally { await CleanupAsync(title); }
    }

    [Theory]
    [InlineData("   ", "title")]
    [InlineData("7", "severity")]
    [InlineData("Low,High", "severity")]
    [InlineData("Unknown", "severity")]
    public async Task T02_InvalidDto_Returns400WithFieldKey(string value, string key)
    {
        var title = key == "title" ? value : $"T02-{Guid.NewGuid():N}";
        var severity = key == "severity" ? value : "Low";
        using var response = await _client.PostAsJsonAsync("/api/incidents", Body(title, severity: severity));
        await AssertValidationAsync(response, key);
    }

    [Fact]
    public async Task T02_MissingOccurredAt_And_FutureDate_Return400()
    {
        using var missing = await _client.PostAsJsonAsync("/api/incidents", new
        {
            title = $"T02-{Guid.NewGuid():N}", description = "Опис", severity = "Low"
        });
        await AssertValidationAsync(missing, "occurredAtUtc");

        using var future = await _client.PostAsJsonAsync("/api/incidents",
            Body($"T02-{Guid.NewGuid():N}", occurredAt: DateTimeOffset.Parse("2099-01-01T00:00:00Z")));
        await AssertValidationAsync(future, "occurredAtUtc");
    }

    [Fact]
    public async Task T02_TooLongTitle_And_Description_Return400()
    {
        using var response = await _client.PostAsJsonAsync("/api/incidents",
            Body(new string('x', 161), new string('y', 4001)));
        var errors = await AssertValidationAsync(response, "title");
        Assert.True(errors.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task T03_DuplicateActiveTitle_Returns409_AndCaseDiffers_Returns201()
    {
        var title = $"Regression-{Guid.NewGuid():N}";
        var lower = title.ToLowerInvariant();
        try
        {
            using var first = await _client.PostAsJsonAsync("/api/incidents", Body(title));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            using var second = await _client.PostAsJsonAsync("/api/incidents", Body("  " + title + " "));
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
            var json = await second.Content.ReadAsStringAsync();
            AssertSafeProblem(json);
            Assert.False(JsonDocument.Parse(json).RootElement.TryGetProperty("errors", out _));

            using var other = await _client.PostAsJsonAsync("/api/incidents", Body(lower));
            Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        }
        finally
        {
            await CleanupAsync(title);
            await CleanupAsync(title.ToLowerInvariant());
        }
    }

    [Fact]
    public async Task T03_ClosedIncidentDoesNotBlockSameTitle()
    {
        var title = $"Closed-{Guid.NewGuid():N}";
        try
        {
            using var first = await _client.PostAsJsonAsync("/api/incidents", Body(title));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
                await db.Incidents.Where(item => item.Title == title)
                    .ExecuteUpdateAsync(set => set.SetProperty(item => item.Status,
                        SecureLab.Api.Data.Entities.IncidentStatus.Closed));
            }
            using var second = await _client.PostAsJsonAsync("/api/incidents", Body(title));
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        }
        finally { await CleanupAsync(title); }
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Critical")]
    public async Task T09_HighSeverity_Description39Fails_40Passes(string severity)
    {
        var title39 = $"T09a-{Guid.NewGuid():N}";
        var title40 = $"T09b-{Guid.NewGuid():N}";
        try
        {
            using var short39 = await _client.PostAsJsonAsync("/api/incidents",
                Body(title39, "  " + new string('x', 39) + "  ", severity));
            await AssertValidationAsync(short39, "description");

            using var ok40 = await _client.PostAsJsonAsync("/api/incidents",
                Body(title40, "  " + new string('x', 40) + "  ", severity));
            Assert.Equal(HttpStatusCode.Created, ok40.StatusCode);
        }
        finally { await CleanupAsync(title39); await CleanupAsync(title40); }
    }

    [Fact]
    public async Task T09_LowSeverity_ShortDescription_IsAllowed()
    {
        var title = $"T09c-{Guid.NewGuid():N}";
        try
        {
            using var response = await _client.PostAsJsonAsync("/api/incidents", Body(title, "коротко", "Low"));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        finally { await CleanupAsync(title); }
    }

    [Fact]
    public async Task T10_OccurredAtOlderThan365Days_Returns400_Within365_Returns201()
    {
        var oldTitle = $"T10a-{Guid.NewGuid():N}";
        var freshTitle = $"T10b-{Guid.NewGuid():N}";
        try
        {
            using var old = await _client.PostAsJsonAsync("/api/incidents",
                Body(oldTitle, occurredAt: DateTimeOffset.UtcNow.AddDays(-366)));
            await AssertValidationAsync(old, "occurredAtUtc");

            using var fresh = await _client.PostAsJsonAsync("/api/incidents",
                Body(freshTitle, occurredAt: DateTimeOffset.UtcNow.AddDays(-364)));
            Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
        }
        finally { await CleanupAsync(oldTitle); await CleanupAsync(freshTitle); }
    }

    [Fact]
    public async Task A02_Overposting_ExtraServerManagedFields_AreIgnored()
    {
        var title = $"A02-{Guid.NewGuid():N}";
        var sentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        try
        {
            using var response = await _client.PostAsJsonAsync("/api/incidents", new
            {
                title,
                description = "Перевірка ігнорування зайвих полів у request DTO.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                id = sentId,
                ownerUserId = DbSeeder.BobId,
                status = "Closed",
                createdAtUtc = "2000-01-01T00:00:00Z"
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.NotEqual(sentId, created.GetProperty("id").GetGuid());
            Assert.Equal("New", created.GetProperty("status").GetString());
            Assert.True(created.GetProperty("createdAtUtc").GetDateTimeOffset().Year >= 2026);

            using var details = await _client.GetAsync(response.Headers.Location);
            var root = JsonDocument.Parse(await details.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("Аліса Коваль", root.GetProperty("ownerDisplayName").GetString());
        }
        finally { await CleanupAsync(title); }
    }
}
