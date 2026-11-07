using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Scaffolding;

namespace SecureLab.Api.Tests;

// Матриця sortBy x direction для GET /api/incidents/search (12 клітинок + додаткові випадки).
public sealed class SearchDirectionTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string Url(string sortBy, string direction)
    {
        var parts = new List<string>();
        if (sortBy.Length > 0) parts.Add("sortBy=" + Uri.EscapeDataString(sortBy));
        if (direction.Length > 0) parts.Add("direction=" + Uri.EscapeDataString(direction));
        return "/api/incidents/search" + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");
    }

    [Theory]
    // sortBy порожній / createdAtUtc: допустимі newest/oldest, невідоме - 400
    [InlineData("", "", 200)]
    [InlineData("", "newest", 200)]
    [InlineData("", "oldest", 200)]
    [InlineData("", "foo", 400)]
    [InlineData("createdAtUtc", "", 200)]
    [InlineData("createdAtUtc", "newest", 200)]
    [InlineData("createdAtUtc", "oldest", 200)]
    [InlineData("createdAtUtc", "foo", 400)]
    // severity / status: direction не сумісний - будь-яке непорожнє значення дає 400
    [InlineData("severity", "", 200)]
    [InlineData("severity", "newest", 400)]
    [InlineData("severity", "oldest", 400)]
    [InlineData("severity", "foo", 400)]
    [InlineData("status", "", 200)]
    [InlineData("status", "newest", 400)]
    [InlineData("status", "oldest", 400)]
    [InlineData("status", "foo", 400)]
    // додаткові: регістр і числа (Enum.TryParse приймає "0", тому вони відхиляються окремо)
    [InlineData("", "NEWEST", 200)]
    [InlineData("createdAtUtc", "Oldest", 200)]
    [InlineData("", "0", 400)]
    [InlineData("", "1", 400)]
    [InlineData("", "newest,oldest", 400)]
    public async Task Matrix_ReturnsExpectedStatus(string sortBy, string direction, int expected)
    {
        using var response = await _client.GetAsync(Url(sortBy, direction));
        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        if (expected == 400)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var json = await response.Content.ReadAsStringAsync();
            Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("errors").TryGetProperty("direction", out _),
                "errors має містити ключ 'direction'");
        }
    }

    [Fact]
    public async Task UnknownSortBy_WithUnknownDirection_ReportsBothKeys()
    {
        using var response = await _client.GetAsync(Url("price", "foo"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("sortBy", out _));
        Assert.True(errors.TryGetProperty("direction", out _));
    }

    [Fact]
    public async Task Newest_IsDescending_AndEqualsDefault()
    {
        var def = await _client.GetFromJsonAsync<List<SearchIncidentResponse>>(Url("", ""));
        var newest = await _client.GetFromJsonAsync<List<SearchIncidentResponse>>(Url("createdAtUtc", "newest"));
        Assert.NotNull(def);
        Assert.NotNull(newest);
        Assert.Equal(def.Select(row => row.Id), newest.Select(row => row.Id));
        for (var i = 1; i < newest.Count; i++)
            Assert.True(newest[i - 1].CreatedAtUtc >= newest[i].CreatedAtUtc, "newest має спадати (DESC)");
    }

    [Fact]
    public async Task Oldest_IsAscending_AndIsReverseOfNewestByDate()
    {
        var newest = await _client.GetFromJsonAsync<List<SearchIncidentResponse>>(Url("", "newest"));
        var oldest = await _client.GetFromJsonAsync<List<SearchIncidentResponse>>(Url("", "oldest"));
        Assert.NotNull(newest);
        Assert.NotNull(oldest);
        Assert.Equal(newest.Count, oldest.Count);
        for (var i = 1; i < oldest.Count; i++)
            Assert.True(oldest[i - 1].CreatedAtUtc <= oldest[i].CreatedAtUtc, "oldest має зростати (ASC)");
        Assert.Equal(newest.Select(row => row.CreatedAtUtc).Reverse(), oldest.Select(row => row.CreatedAtUtc));
    }
}
