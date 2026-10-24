using ShipFlow.Domain.Common;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Companies;

/// <summary>
/// Aggregate root for a transport company and its memberships. It is the tenant boundary: drivers and vehicles
/// are created through it so they always belong to a valid, active company.
/// </summary>
public sealed class TransportCompany : Entity
{
    public const int MaxNameLength = 200;

    private readonly List<CompanyMembership> _memberships = [];

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private TransportCompany()
    {
        Name = null!;
    }

    private TransportCompany(string name, DateTime nowUtc)
        : base(Guid.NewGuid())
    {
        Name = Guard.NotBlank(name, nameof(name), MaxNameLength);
        CreatedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        IsActive = true;
    }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public IReadOnlyList<CompanyMembership> Memberships => _memberships;

    /// <summary>Creates a company with its first administrator so the "at least one admin" invariant holds from the start.</summary>
    public static TransportCompany Create(string name, Guid adminUserId, DateTime nowUtc)
    {
        var company = new TransportCompany(name, nowUtc);
        company._memberships.Add(new CompanyMembership(company.Id, adminUserId, CompanyRole.Admin, nowUtc));
        return company;
    }

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(name), MaxNameLength);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public bool HasActiveMember(Guid userId, CompanyRole? role = null) =>
        _memberships.Any(m => m.IsActive && m.UserId == userId && (role is null || m.Role == role));

    public CompanyMembership AddMember(Guid userId, CompanyRole role, DateTime nowUtc)
    {
        EnsureActive();
        if (_memberships.Any(m => m.IsActive && m.UserId == userId))
        {
            throw new DomainException(DomainErrorCodes.Validation, "The user is already an active member of this company.");
        }

        var membership = new CompanyMembership(Id, userId, role, nowUtc);
        _memberships.Add(membership);
        return membership;
    }

    public void RemoveMember(Guid userId, DateTime nowUtc)
    {
        var membership = _memberships.FirstOrDefault(m => m.IsActive && m.UserId == userId)
            ?? throw new DomainException(DomainErrorCodes.NotFound, "The user is not an active member of this company.");

        if (membership.Role == CompanyRole.Admin &&
            _memberships.Count(m => m.IsActive && m.Role == CompanyRole.Admin) == 1)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, "A company must keep at least one active administrator.");
        }

        membership.Deactivate(nowUtc);
    }

    /// <summary>Registers a driver. The user must be an active Driver member of this (active) company.</summary>
    public Driver RegisterDriver(Guid userId, string fullName, string licenseNumber)
    {
        EnsureActive();
        if (!HasActiveMember(userId, CompanyRole.Driver))
        {
            throw new DomainException(
                DomainErrorCodes.CrossCompany,
                "The user must be an active member of this company with the Driver role.");
        }

        return new Driver(Id, userId, fullName, licenseNumber);
    }

    /// <summary>
    /// Re-activates a driver of this company. Only allowed while the driver's user is still an active Driver member,
    /// so removing a member (which deactivates the driver) cannot be undone by activating the driver alone.
    /// </summary>
    public void ActivateDriver(Driver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        EnsureActive();
        if (driver.CompanyId != Id)
        {
            throw new DomainException(DomainErrorCodes.CrossCompany, "The driver belongs to a different company.");
        }

        if (!HasActiveMember(driver.UserId, CompanyRole.Driver))
        {
            throw new DomainException(
                DomainErrorCodes.InvalidState,
                "The driver is no longer an active Driver member of this company and cannot be activated.");
        }

        driver.Activate();
    }

    /// <summary>Registers a vehicle owned by this (active) company.</summary>
    public Vehicle RegisterVehicle(string plateNumber, VehicleType type, Capacity capacity)
    {
        EnsureActive();
        return new Vehicle(Id, plateNumber, type, capacity);
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, "The transport company is not active.");
        }
    }
}
