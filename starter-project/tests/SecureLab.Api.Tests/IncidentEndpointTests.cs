using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentEndpointTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetList_ReturnsSeededIncidents()
    {
        var incidents = await _client.GetFromJsonAsync<List<IncidentListItemResponse>>(
            "/api/incidents");

        Assert.NotNull(incidents);
        Assert.Contains(incidents, incident => incident.Id == DbSeeder.AliceIncidentId);
        Assert.Contains(incidents, incident => incident.Id == DbSeeder.BobIncidentId);
    }

    [Fact]
    public async Task GetDetails_ForUnknownId_ReturnsProblemDetails404()
    {
        using var response = await _client.GetAsync($"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDetails_DoesNotExposeInternalOwnerFields()
    {
        using var response = await _client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(document.RootElement.TryGetProperty("ownerUserId", out _));
        Assert.False(document.RootElement.TryGetProperty("email", out _));
        Assert.Equal("Аліса Коваль", document.RootElement.GetProperty("ownerDisplayName").GetString());
    }

    [Fact]
    public async Task ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        var script = await _client.GetStringAsync("/app.js");

        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.Contains("textContent", script, StringComparison.Ordinal);
    }

    private async Task<JsonElement[]> GetSummaryAsync(string url)
    {
        using var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
    }

    [Fact]
    public async Task SeveritySummary_WithoutStatus_CountsAllSeededIncidents()
    {
        var items = await GetSummaryAsync("/api/incidents/severity-summary");

        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.True(item.TryGetProperty("severity", out _));
            Assert.True(item.TryGetProperty("count", out _));
        });
        Assert.Equal(3, items.Sum(item => item.GetProperty("count").GetInt32()));
    }

    [Fact]
    public async Task SeveritySummary_WithTriaged_CountsOnlyTriagedIncidents()
    {
        var items = await GetSummaryAsync("/api/incidents/severity-summary?status=Triaged");

        Assert.Equal(1, items.Sum(item => item.GetProperty("count").GetInt32()));
    }

    [Fact]
    public async Task SeveritySummary_WithResolved_ReturnsZeroCounts()
    {
        var items = await GetSummaryAsync("/api/incidents/severity-summary?status=Resolved");

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(0, item.GetProperty("count").GetInt32()));
    }

    [Fact]
    public async Task SeveritySummary_WithUnknownStatus_ReturnsValidationProblem400()
    {
        using var response = await _client.GetAsync("/api/incidents/severity-summary?status=Unknown");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
