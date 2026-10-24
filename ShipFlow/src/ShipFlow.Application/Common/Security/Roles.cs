namespace ShipFlow.Application.Common.Security;

/// <summary>Platform roles stored in ASP.NET Core Identity and carried in the JWT <c>role</c> claim.</summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string CompanyAdmin = "CompanyAdmin";
    public const string Dispatcher = "Dispatcher";
    public const string Driver = "Driver";
    public const string PlatformAdmin = "PlatformAdmin";

    public static readonly IReadOnlyList<string> All =
        [Customer, CompanyAdmin, Dispatcher, Driver, PlatformAdmin];

    /// <summary>
    /// The only role public registration can ever grant. Privileged roles are granted by other flows
    /// (company onboarding in Phase 4, platform administration) and never from request data.
    /// </summary>
    public const string SelfRegistration = Customer;
}

/// <summary>Authorization policy names. The policies are defined in the API composition root.</summary>
public static class Policies
{
    public const string RequireCustomer = nameof(RequireCustomer);
    public const string RequireCompanyAdmin = nameof(RequireCompanyAdmin);
    public const string RequireDispatcher = nameof(RequireDispatcher);
    public const string RequireDriver = nameof(RequireDriver);
    public const string RequireCompanyStaff = nameof(RequireCompanyStaff);
    public const string RequirePlatformAdmin = nameof(RequirePlatformAdmin);

    /// <summary>Customers, company administrators and dispatchers, platform admins: callers who list or read shipments.</summary>
    public const string RequireShipmentViewer = nameof(RequireShipmentViewer);
}

/// <summary>
/// The single mapping between a company role and the Identity role that appears in the token. Memberships are the source
/// of truth; the Identity role is derived from them (docs/DECISIONS.md D-023).
/// </summary>
public static class CompanyRoleMapping
{
    public static string ToIdentityRole(Domain.Companies.CompanyRole role) => role switch
    {
        Domain.Companies.CompanyRole.Admin => Roles.CompanyAdmin,
        Domain.Companies.CompanyRole.Dispatcher => Roles.Dispatcher,
        Domain.Companies.CompanyRole.Driver => Roles.Driver,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown company role.")
    };
}
