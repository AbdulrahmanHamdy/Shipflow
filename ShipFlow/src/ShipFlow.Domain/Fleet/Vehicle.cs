using ShipFlow.Domain.Common;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Fleet;

public enum VehicleType
{
    Van = 0,
    Pickup = 1,
    Truck = 2,
    Trailer = 3
}

/// <summary>
/// A vehicle owned by exactly one transport company. Created through <c>TransportCompany.RegisterVehicle</c>.
/// </summary>
public sealed class Vehicle : Entity
{
    public const int MaxPlateLength = 20;

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private Vehicle()
    {
        PlateNumber = null!;
                Capacity = null!;
    }

    internal Vehicle(Guid companyId, string plateNumber, VehicleType type, Capacity capacity)
        : base(Guid.NewGuid())
    {
        CompanyId = Guard.NotEmpty(companyId, nameof(companyId));
        PlateNumber = NormalizePlate(plateNumber);
        if (!Enum.IsDefined(type))
        {
            throw Guard.Invalid("Unknown vehicle type.");
        }

        Type = type;
        Capacity = Guard.NotNull(capacity, nameof(capacity));
        IsActive = true;
    }

    public Guid CompanyId { get; }

    /// <summary>Normalized (trimmed, upper-case, inner spaces removed). Unique per company at the persistence level.</summary>
    public string PlateNumber { get; }

    public VehicleType Type { get; }

    public Capacity Capacity { get; private set; }

    /// <summary>Whether the vehicle is in service at all. This is NOT booking availability.</summary>
    public bool IsActive { get; private set; }

    public void UpdateCapacity(Capacity capacity) => Capacity = Guard.NotNull(capacity, nameof(capacity));

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    internal static string NormalizePlate(string plateNumber)
    {
        var trimmed = Guard.NotBlank(plateNumber, nameof(plateNumber), MaxPlateLength);
        return string.Concat(trimmed.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }
}
