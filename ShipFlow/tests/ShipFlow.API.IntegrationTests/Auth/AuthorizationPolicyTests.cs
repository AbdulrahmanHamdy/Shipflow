using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ShipFlow.API.IntegrationTests.Support;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.API.IntegrationTests.Auth;

/// <summary>Role boundaries, checked against the real policy configuration of the API.</summary>
public class AuthorizationPolicyTests : IClassFixture<ShipFlowApiFactory>
{
    private static readonly string[] AllPolicies =
    [
        Policies.RequireCustomer, Policies.RequireCompanyAdmin, Policies.RequireDispatcher,
        Policies.RequireDriver, Policies.RequireCompanyStaff, Policies.RequirePlatformAdmin, Policies.RequireShipmentViewer
    ];

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [Roles.Customer] = [Policies.RequireCustomer, Policies.RequireShipmentViewer],
        [Roles.CompanyAdmin] = [Policies.RequireCompanyAdmin, Policies.RequireDispatcher, Policies.RequireCompanyStaff, Policies.RequireShipmentViewer],
        [Roles.Dispatcher] = [Policies.RequireDispatcher, Policies.RequireCompanyStaff, Policies.RequireShipmentViewer],
        [Roles.Driver] = [Policies.RequireDriver, Policies.RequireCompanyStaff],
        [Roles.PlatformAdmin] = [Policies.RequirePlatformAdmin, Policies.RequireShipmentViewer]
    };

    private readonly ShipFlowApiFactory _factory;

    public AuthorizationPolicyTests(ShipFlowApiFactory factory) => _factory = factory;

    public static IEnumerable<object[]> RoleAndPolicy() =>
        Roles.All.SelectMany(role => AllPolicies.Select(policy => new object[] { role, policy }));

    [Theory]
    [MemberData(nameof(RoleAndPolicy))]
    public async Task Policy_AllowsExactlyTheIntendedRoles(string role, string policy)
    {
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var user = Principal(role);

        var result = await authorization.AuthorizeAsync(user, null, policy);

        Assert.Equal(Allowed[role].Contains(policy), result.Succeeded);
    }

    [Theory]
    [InlineData(Policies.RequireCustomer)]
    [InlineData(Policies.RequireCompanyAdmin)]
    [InlineData(Policies.RequireDispatcher)]
    [InlineData(Policies.RequireDriver)]
    [InlineData(Policies.RequireCompanyStaff)]
    [InlineData(Policies.RequirePlatformAdmin)]
    [InlineData(Policies.RequireShipmentViewer)]
    public async Task Policy_DeniesAnonymousAndRolelessUsers(string policy)
    {
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var anonymous = await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy);
        var noRole = await authorization.AuthorizeAsync(Principal(), null, policy);

        Assert.False(anonymous.Succeeded);
        Assert.False(noRole.Succeeded);
    }

    [Fact]
    public async Task FallbackPolicy_RequiresAnAuthenticatedUser()
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.True(fallback!.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Any());
    }

    [Fact]
    public void OnlyTheIntendedEndpoints_AreAnonymous()
    {
        // Anything not on this list needs a valid token (fallback policy). Adding an anonymous endpoint must be a
        // conscious change to this test.
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var anonymous = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "/api/auth/login", "/api/auth/register", "/api/payments/callback", "/api/payments/callback", "/health", "/health/ready" },
            anonymous);
    }

    private static ClaimsPrincipal Principal(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimNames.Subject, Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimNames.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimNames.Subject, ClaimNames.Role));
    }
}
