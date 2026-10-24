using Microsoft.AspNetCore.Authorization;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.API.Auth;

public static class AuthorizationSetup
{
    /// <summary>
    /// Role policies plus a secure default: every endpoint requires an authenticated user unless it is explicitly
    /// marked <c>[AllowAnonymous]</c>. Roles only answer "what kind of user is this"; ownership and company
    /// isolation are checked per resource in the use cases (Phases 3-5).
    /// </summary>
    public static IServiceCollection AddShipFlowAuthorization(this IServiceCollection services)
    {
        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.RequireCustomer, p => p.RequireRole(Roles.Customer))
            .AddPolicy(Policies.RequireCompanyAdmin, p => p.RequireRole(Roles.CompanyAdmin))
            .AddPolicy(Policies.RequireDispatcher, p => p.RequireRole(Roles.CompanyAdmin, Roles.Dispatcher))
            .AddPolicy(Policies.RequireDriver, p => p.RequireRole(Roles.Driver))
            .AddPolicy(Policies.RequireCompanyStaff, p => p.RequireRole(Roles.CompanyAdmin, Roles.Dispatcher, Roles.Driver))
            .AddPolicy(Policies.RequirePlatformAdmin, p => p.RequireRole(Roles.PlatformAdmin))
            .AddPolicy(Policies.RequireShipmentViewer, p => p.RequireRole(Roles.Customer, Roles.CompanyAdmin, Roles.Dispatcher, Roles.PlatformAdmin));

        return services;
    }
}
