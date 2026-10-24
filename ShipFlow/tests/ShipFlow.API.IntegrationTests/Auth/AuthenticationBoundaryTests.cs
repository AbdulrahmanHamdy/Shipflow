using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipFlow.API.IntegrationTests.Support;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.API.IntegrationTests.Auth;

/// <summary>JWT validation at the HTTP boundary. None of these tests touch the database.</summary>
public class AuthenticationBoundaryTests : IClassFixture<ShipFlowApiFactory>
{
    private readonly ShipFlowApiFactory _factory;

    public AuthenticationBoundaryTests(ShipFlowApiFactory factory) => _factory = factory;

    private async Task<HttpResponseMessage> GetMe(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.GetAsync("/api/auth/me");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMe(null)).StatusCode);

    [Fact]
    public async Task Me_WithGarbageToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMe("not.a.jwt")).StatusCode);

    [Fact]
    public async Task Me_WithTokenSignedByAnotherKey_Returns401() =>
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await GetMe(TestTokens.Create([Roles.Customer], signingKey: new string('k', 48)))).StatusCode);

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        var now = DateTime.UtcNow;
        var token = TestTokens.Create([Roles.Customer], notBefore: now.AddMinutes(-30), expires: now.AddMinutes(-10));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMe(token)).StatusCode);
    }

    [Fact]
    public async Task Me_WithNotYetValidToken_Returns401()
    {
        var now = DateTime.UtcNow;
        var token = TestTokens.Create([Roles.Customer], notBefore: now.AddMinutes(10), expires: now.AddMinutes(20));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMe(token)).StatusCode);
    }

    [Fact]
    public async Task Me_WithWrongAudience_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMe(TestTokens.Create([Roles.Customer], audience: "someone-else"))).StatusCode);

    [Fact]
    public async Task Me_WithWrongIssuer_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMe(TestTokens.Create([Roles.Customer], issuer: "someone-else"))).StatusCode);

    [Fact]
    public async Task Me_WithUnsignedAlgNoneToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMe(TestTokens.Unsigned([Roles.PlatformAdmin]))).StatusCode);

    [Fact]
    public async Task Me_WithValidToken_ReturnsIdentityFromTheTokenClaims()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Create([Roles.Dispatcher, Roles.Driver], userId: userId, email: "dana@example.com");

        var response = await GetMe(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, doc.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("dana@example.com", doc.RootElement.GetProperty("email").GetString());
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray().Select(e => e.GetString()).OrderBy(r => r, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { Roles.Dispatcher, Roles.Driver }, roles);
    }

    [Fact]
    public async Task Me_WithTokenIssuedByTheTokenService_IsAccepted()
    {
        // Proves the service that signs tokens and the middleware that validates them agree on key, issuer and claims.
        var tokens = _factory.Services.GetRequiredService<ITokenService>();
        var user = new AuthenticatedUser(Guid.NewGuid(), "ada@example.com", "Ada", [Roles.Customer]);

        var issued = tokens.CreateAccessToken(user);
        var response = await GetMe(issued.Value);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Id, body.GetProperty("id").GetGuid());
        Assert.True(issued.ExpiresAtUtc > DateTime.UtcNow);
    }
}
