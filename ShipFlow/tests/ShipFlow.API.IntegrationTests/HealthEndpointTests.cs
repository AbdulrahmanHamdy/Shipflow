using System.Net;
using System.Text.Json;
using ShipFlow.API.IntegrationTests.Support;

namespace ShipFlow.API.IntegrationTests;

public class HealthEndpointTests : IClassFixture<ShipFlowApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ShipFlowApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetHealth_ReturnsOk_WithHealthyStatusJson_WithoutCredentials()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("timestampUtc", out _));
    }

    [Fact]
    public async Task GetHealth_DoesNotDependOnTheDatabase()
    {
        // The test factory points at a database that does not exist; liveness must not care.
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUnknownRoute_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
