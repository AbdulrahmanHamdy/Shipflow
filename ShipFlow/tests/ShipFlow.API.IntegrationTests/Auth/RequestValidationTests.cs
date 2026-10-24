using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ShipFlow.API.IntegrationTests.Support;

namespace ShipFlow.API.IntegrationTests.Auth;

/// <summary>
/// Validation and error-shape checks. Requests are rejected before any database access, so these run without SQL Server.
/// </summary>
public class RequestValidationTests : IClassFixture<ShipFlowApiFactory>
{
    private readonly HttpClient _client;

    public RequestValidationTests(ShipFlowApiFactory factory) => _client = factory.CreateClient();

    private static async Task<JsonElement> ReadProblem(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task Register_WithEmptyBody_Returns400_ListingEveryMissingField()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblem(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("validation", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
        Assert.True(errors.TryGetProperty("FullName", out _));
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400_OnlyForPassword()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "ada@example.com", password = "weak", fullName = "Ada" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblem(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Password", out _));
        Assert.False(errors.TryGetProperty("Email", out _));
        Assert.False(errors.TryGetProperty("FullName", out _));
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "not-an-email", password = "Str0ngPassw0rd", fullName = "Ada" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadProblem(response)).GetProperty("errors").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task Register_WithMalformedJson_Returns400()
    {
        using var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithNonJsonContentType_Returns415()
    {
        using var content = new StringContent("email=a@b.com", Encoding.UTF8, "text/plain");

        var response = await _client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblem(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task ErrorResponses_NeverLeakExceptionDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { });
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain(" at ShipFlow.", body);
    }
}
