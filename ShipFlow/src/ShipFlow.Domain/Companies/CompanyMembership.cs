using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.Companies;

/// <summary>Roles a user can hold inside a transport company.</summary>
public enum CompanyRole
{
    Admin = 0,
    Dispatcher = 1,
    Driver = 2
}

/// <summary>Links a user to a transport company with a company role. Owned by <see cref="TransportCompany"/>.</summary>
public sealed class CompanyMembership : Entity
{
    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private CompanyMembership()
    {
    }

    internal CompanyMembership(Guid companyId, Guid userId, CompanyRole role, DateTime nowUtc)
        : base(Guid.NewGuid())
    {
        CompanyId = Guard.NotEmpty(companyId, nameof(companyId));
        UserId = Guard.NotEmpty(userId, nameof(userId));
        if (!Enum.IsDefined(role))
        {
            throw Guard.Invalid("Unknown company role.");
        }

        Role = role;
        JoinedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        IsActive = true;
    }

    public Guid CompanyId { get; }

    public Guid UserId { get; }

    public CompanyRole Role { get; }

    public bool IsActive { get; private set; }

    public DateTime JoinedAtUtc { get; }

    public DateTime? LeftAtUtc { get; private set; }

    internal void Deactivate(DateTime nowUtc)
    {
        IsActive = false;
        LeftAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
    }
}
