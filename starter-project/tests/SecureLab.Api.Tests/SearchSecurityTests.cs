using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Scaffolding;

namespace SecureLab.Api.Tests;

// ЛР 02: S-02, T-04, T-05 та перевірка порядку сортування для GET /api/incidents/search.
public sealed class SearchSecurityTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private const string Id1 = "20000000-0000-0000-0000-000000000001";
    private const string Id2 = "20000000-0000-0000-0000-000000000002";
    private const string Id3 = "20000000-0000-0000-0000-000000000003";
    private const string Id4 = "20000000-0000-0000-0000-000000000004";
    private const string Id5 = "20000000-0000-0000-0000-000000000005";

    private async Task<List<SearchIncidentResponse>> SearchAsync(string query)
    {
        using var response = await _client.GetAsync("/api/incidents/search" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<SearchIncidentResponse>>();
        Assert.NotNull(rows);
        return rows;
    }

    private static string Q(string value) => "?q=" + Uri.EscapeDataString(value);

    private async Task<Guid> CreateAsync(string title)
    {
        using var response = await _client.PostAsJsonAsync("/api/incidents", new
        {
            title,
            description = "Навчальний опис для перевірки literal-пошуку.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private async Task DeleteAsync(params Guid[] ids)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        await db.Incidents.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
    }

    // S-02: той самий контрольний ввід, що й S-01, більше не змінює логіку запиту.
    [Fact]
    public async Task S02_ControlInput_ReturnsEmptySet()
    {
        var rows = await SearchAsync(Q("zz-no-match' OR TRUE -- "));
        Assert.Empty(rows);
    }

    // Позитивний контроль: виправлення не зробило пошук надмірно суворим; точна множина.
    [Fact]
    public async Task S02_PositiveControl_UsbReturnsExactlyOneKnownRecord()
    {
        var rows = await SearchAsync(Q("USB"));
        var single = Assert.Single(rows);
        Assert.Equal(Guid.Parse(Id5), single.Id);
        Assert.Equal("Перевірка USB навчальної мережі", single.Title);
        Assert.DoesNotContain(rows, row => row.Id == Guid.Parse(Id1));
    }

    [Fact]
    public async Task S02_NoMatchValue_ReturnsEmptySet()
    {
        Assert.Empty(await SearchAsync(Q("zz-no-match")));
    }

    // T-04: звичайний пошук і пошук з апострофом (U+0027) без 500.
    [Theory]
    [InlineData("O'Brien", Id4)]
    [InlineData("комп'ютерного", Id3)]
    [InlineData("usb", Id5)]
    public async Task T04_ApostropheAndNormalSearch_ReturnExpectedRecord(string query, string expectedId)
    {
        var rows = await SearchAsync(Q(query));
        var single = Assert.Single(rows);
        Assert.Equal(Guid.Parse(expectedId), single.Id);
    }

    [Fact]
    public async Task T04_PercentSign_IsLiteral_NotWildcard()
    {
        Assert.Empty(await SearchAsync("?q=%25"));
    }

    [Fact]
    public async Task T04_Wildcards_And_Backslash_AreMatchedLiterally()
    {
        var marker = Guid.NewGuid().ToString("N");
        var literal = await CreateAsync($"{marker}-rate_5%");
        var decoy = await CreateAsync($"{marker}-rateX500");
        var path = await CreateAsync($"{marker}-path\\A");
        var decoyPath = await CreateAsync($"{marker}-pathZA");
        try
        {
            var percent = await SearchAsync(Q($"{marker}-rate_5%"));
            Assert.Equal(new[] { literal }, percent.Select(row => row.Id).ToArray());

            var underscore = await SearchAsync(Q($"{marker}-rate_"));
            Assert.Equal(new[] { literal }, underscore.Select(row => row.Id).ToArray());

            var backslash = await SearchAsync(Q($"{marker}-path\\A"));
            Assert.Equal(new[] { path }, backslash.Select(row => row.Id).ToArray());
            Assert.DoesNotContain(backslash, row => row.Id == decoyPath);
            Assert.DoesNotContain(percent, row => row.Id == decoy);
        }
        finally { await DeleteAsync(literal, decoy, path, decoyPath); }
    }

    [Theory]
    [InlineData("price")]
    [InlineData("title; DROP TABLE incidents")]
    [InlineData("severity DESC, id")]
    [InlineData("created_at_utc")]
    public async Task T05_UnknownSortBy_Returns400WithSortByKey(string sortBy)
    {
        using var response = await _client.GetAsync("/api/incidents/search?sortBy=" + Uri.EscapeDataString(sortBy));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));
        foreach (var forbidden in new[] { "Exception", "Npgsql", "SELECT", "StackTrace" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", new[] { Id4, Id5, Id3, Id2, Id1 })]
    [InlineData("?sortBy=createdAtUtc", new[] { Id4, Id5, Id3, Id2, Id1 })]
    [InlineData("?sortBy=severity", new[] { Id2, Id1, Id3, Id4, Id5 })]
    [InlineData("?sortBy=status", new[] { Id3, Id4, Id5, Id1, Id2 })]
    public async Task Sorting_FollowsContractRanksWithIdAsSecondKey(string query, string[] expected)
    {
        var rows = await SearchAsync(query);
        Assert.Equal(expected.Select(Guid.Parse).ToArray(), rows.Select(row => row.Id).ToArray());
    }

    [Fact]
    public void EscapeLike_EscapesBackslashFirst_ThenWildcards()
    {
        Assert.Equal("rate\\_5\\%", Lab02Endpoints.EscapeLike("rate_5%"));
        Assert.Equal("path\\\\A", Lab02Endpoints.EscapeLike("path\\A"));
        Assert.Equal("O'Brien", Lab02Endpoints.EscapeLike("O'Brien"));
    }
}
