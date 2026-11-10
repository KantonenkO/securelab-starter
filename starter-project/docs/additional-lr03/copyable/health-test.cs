// Навчальний метод: вставте всередину наявного IncidentEndpointTests.
[Fact]
public async Task Health_WhenDatabaseIsAvailable_ReturnsReady()
{
    using var response = await _client.GetAsync("/health");
    var json = await response.Content.ReadAsStringAsync();
    using var document = JsonDocument.Parse(json);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    Assert.Equal("ready", document.RootElement.GetProperty("status").GetString());
}
