using Microsoft.AspNetCore.Identity;

namespace ShipFlow.Infrastructure.Identity;

/// <summary>
/// A platform account (ASP.NET Core Identity, Guid key). The domain refers to users by id only.
/// Company membership is not stored here: it lives in <c>CompanyMembership</c>.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int MaxFullNameLength = 150;

    public string FullName { get; set; } = string.Empty;

    /// <summary>A deactivated account can never sign in, whatever its password.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string name) : base(name)
    {
    }
}
