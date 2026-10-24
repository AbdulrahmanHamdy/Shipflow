using ShipFlow.Domain.Common;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Shipments;

public enum StopType
{
    Pickup = 0,
    Waypoint = 1,
    Delivery = 2
}

/// <summary>An ordered stop of a shipment. Created only through <see cref="Shipment.Create"/>.</summary>
public sealed class ShipmentStop : Entity
{
    public const int MaxAddressLength = 300;

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private ShipmentStop()
    {
        Address = null!;
                Location = null!;
    }

    internal ShipmentStop(int sequence, StopType type, string address, GeoPoint location, TimeWindow? window)
        : base(Guid.NewGuid())
    {
        Sequence = sequence;
        Type = type;
        Address = Guard.NotBlank(address, nameof(address), MaxAddressLength);
        Location = Guard.NotNull(location, nameof(location));
        Window = window;
    }

    public int Sequence { get; }

    public StopType Type { get; }

    public string Address { get; }

    public GeoPoint Location { get; }

    public TimeWindow? Window { get; }
}
