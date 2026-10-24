using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tracking;

/// <summary>A GPS position reported while a shipment is moving. High volume: kept outside the Shipment aggregate.</summary>
public sealed class TrackingPoint : Entity
{
    /// <summary>How far in the future a device timestamp may be before it is rejected as clock skew.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private TrackingPoint()
    {
        Location = null!;
    }

    private TrackingPoint(
        Guid shipmentId,
        Guid recordedByUserId,
        GeoPoint location,
        DateTime recordedAtUtc,
        decimal? speedKph)
        : base(Guid.NewGuid())
    {
        ShipmentId = shipmentId;
        RecordedByUserId = Guard.NotEmpty(recordedByUserId, nameof(recordedByUserId));
        Location = Guard.NotNull(location, nameof(location));
        RecordedAtUtc = recordedAtUtc;
        SpeedKph = speedKph;
    }

    public Guid ShipmentId { get; }

    public Guid RecordedByUserId { get; }

    public GeoPoint Location { get; }

    public DateTime RecordedAtUtc { get; }

    public decimal? SpeedKph { get; }

    /// <summary>Only shipments that are PickedUp or InTransit accept tracking points.</summary>
    public static TrackingPoint Create(
        Shipment shipment,
        Guid recordedByUserId,
        GeoPoint location,
        DateTime recordedAtUtc,
        DateTime nowUtc,
        decimal? speedKph = null)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        Guard.Utc(recordedAtUtc, nameof(recordedAtUtc));
        Guard.Utc(nowUtc, nameof(nowUtc));

        if (shipment.Status is not (ShipmentStatus.PickedUp or ShipmentStatus.InTransit))
        {
            throw new DomainException(
                DomainErrorCodes.InvalidState,
                $"Tracking points can only be added to a PickedUp or InTransit shipment (current: {shipment.Status}).");
        }

        if (recordedAtUtc > nowUtc + MaxClockSkew)
        {
            throw Guard.Invalid("The recorded time is in the future.");
        }

        if (speedKph is < 0m)
        {
            throw Guard.Invalid("Speed must not be negative.");
        }

        return new TrackingPoint(shipment.Id, recordedByUserId, location, recordedAtUtc, speedKph);
    }
}
