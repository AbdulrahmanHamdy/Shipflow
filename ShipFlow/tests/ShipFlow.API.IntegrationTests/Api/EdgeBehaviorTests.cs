using System.Net;
using System.Net.Http.Json;
using ShipFlow.API.IntegrationTests.Support;

namespace ShipFlow.API.IntegrationTests.Api;

public class RateLimitingTests : IClassFixture<ShipFlowApiFactory>
{
    private readonly ShipFlowApiFactory _factory;

    public RateLimitingTests(ShipFlowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AuthEndpoints_AreRateLimited_AfterTheConfiguredPermits()
    {
        var client = _factory
            .WithSettings(new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "3", ["RateLimiting:AuthWindowSeconds"] = "600" })
            .CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            // An empty body is rejected by validation (400) without touching a database; the limiter runs first.
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", new { })).StatusCode);
        }

        Assert.Equal(
            new[]
            {
                HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest,
                HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests
            },
            statuses);

        var limited = await client.PostAsJsonAsync("/api/auth/register", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NonAuthEndpoints_AreNotAffectedByTheAuthLimit()
    {
        var client = _factory
            .WithSettings(new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "1", ["RateLimiting:AuthWindowSeconds"] = "600" })
            .CreateClient();

        await client.PostAsJsonAsync("/api/auth/login", new { });
        await client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}

public class CorsTests : IClassFixture<ShipFlowApiFactory>
{
    private const string AllowedOrigin = "https://app.example.com";

    private readonly ShipFlowApiFactory _factory;

    public CorsTests(ShipFlowApiFactory factory) => _factory = factory;

    private static HttpRequestMessage Get(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);
        return request;
    }

    [Fact]
    public async Task ByDefault_NoOriginIsAllowed()
    {
        var response = await _factory.CreateClient().SendAsync(Get(AllowedOrigin));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ConfiguredOrigin_IsAllowed_AndOthersAreNot()
    {
        var client = _factory
            .WithSettings(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin })
            .CreateClient();

        var allowed = await client.SendAsync(Get(AllowedOrigin));
        var denied = await client.SendAsync(Get("https://evil.example"));

        Assert.Equal(AllowedOrigin, Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(allowed.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_ForConfiguredOrigin_IsAnswered()
    {
        var client = _factory
            .WithSettings(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin })
            .CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}

public class SwaggerTests : IClassFixture<ShipFlowApiFactory>
{
    private readonly ShipFlowApiFactory _factory;

    public SwaggerTests(ShipFlowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Swagger_IsOff_OutsideDevelopment_UnlessEnabled()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_WhenEnabled_DocumentsTheAuthEndpoints_AndBearerScheme()
    {
        var client = _factory.WithSettings(new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" }).CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/auth/register", json);
        Assert.Contains("/api/auth/login", json);
        Assert.Contains("/api/auth/me", json);
        Assert.Contains("\"bearer\"", json);
    }
}
