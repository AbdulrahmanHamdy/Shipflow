using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.Fleet;

/// <summary>
/// A driver employed by exactly one transport company. Created through
/// <c>TransportCompany.RegisterDriver</c>, which verifies the company relationship.
/// </summary>
public sealed class Driver : Entity
{
    public const int MaxNameLength = 150;
    public const int MaxLicenseLength = 50;

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private Driver()
    {
        FullName = null!;
                LicenseNumber = null!;
    }

    internal Driver(Guid companyId, Guid userId, string fullName, string licenseNumber)
        : base(Guid.NewGuid())
    {
        CompanyId = Guard.NotEmpty(companyId, nameof(companyId));
        UserId = Guard.NotEmpty(userId, nameof(userId));
        FullName = Guard.NotBlank(fullName, nameof(fullName), MaxNameLength);
        LicenseNumber = Guard.NotBlank(licenseNumber, nameof(licenseNumber), MaxLicenseLength).ToUpperInvariant();
        IsActive = true;
    }

    public Guid CompanyId { get; }

    /// <summary>The identity user this driver signs in as.</summary>
    public Guid UserId { get; }

    public string FullName { get; private set; }

    public string LicenseNumber { get; private set; }

    /// <summary>Whether the driver is employed/usable at all. This is NOT booking availability.</summary>
    public bool IsActive { get; private set; }

    public void UpdateDetails(string fullName, string licenseNumber)
    {
        FullName = Guard.NotBlank(fullName, nameof(fullName), MaxNameLength);
        LicenseNumber = Guard.NotBlank(licenseNumber, nameof(licenseNumber), MaxLicenseLength).ToUpperInvariant();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
