using System.Collections.ObjectModel;
using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.Shipments;

/// <summary>Immutable audit record of a shipment status transition.</summary>
public sealed class ShipmentEvent : Entity
{
    private static readonly IReadOnlyDictionary<string, string> NoMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private ShipmentEvent()
    {
        Metadata = NoMetadata;
    }

    internal ShipmentEvent(
        Guid shipmentId,
        DateTime occurredAtUtc,
        Guid actorId,
        ShipmentStatus? previousStatus,
        ShipmentStatus newStatus,
        IDictionary<string, string>? metadata)
        : base(Guid.NewGuid())
    {
        ShipmentId = Guard.NotEmpty(shipmentId, nameof(shipmentId));
        OccurredAtUtc = Guard.Utc(occurredAtUtc, nameof(occurredAtUtc));
        ActorId = Guard.NotEmpty(actorId, nameof(actorId));
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Metadata = metadata is { Count: > 0 }
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata))
            : NoMetadata;
    }

    public Guid ShipmentId { get; }

    public DateTime OccurredAtUtc { get; }

    /// <summary>User id of the actor, or the company id when a carrier acts on behalf of the company.</summary>
    public Guid ActorId { get; }

    /// <summary>Null only for the creation event.</summary>
    public ShipmentStatus? PreviousStatus { get; }

    public ShipmentStatus NewStatus { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}
