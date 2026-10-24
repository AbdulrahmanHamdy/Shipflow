namespace ShipFlow.Domain.Shipments;

/// <summary>The single source of truth for allowed shipment status transitions (see docs/ARCHITECTURE.md).</summary>
public static class ShipmentLifecycle
{
    private static readonly Dictionary<ShipmentStatus, ShipmentStatus[]> Allowed = new()
    {
        [ShipmentStatus.Pending] = [ShipmentStatus.Quoting, ShipmentStatus.Cancelled],
        [ShipmentStatus.Quoting] = [ShipmentStatus.Accepted, ShipmentStatus.Cancelled],
        [ShipmentStatus.Accepted] = [ShipmentStatus.Assigned, ShipmentStatus.Cancelled],
        [ShipmentStatus.Assigned] = [ShipmentStatus.Accepted, ShipmentStatus.PickedUp, ShipmentStatus.Cancelled],
        [ShipmentStatus.PickedUp] = [ShipmentStatus.InTransit, ShipmentStatus.DeliveryFailed],
        [ShipmentStatus.InTransit] = [ShipmentStatus.Delivered, ShipmentStatus.DeliveryFailed],
        [ShipmentStatus.DeliveryFailed] = [ShipmentStatus.InTransit, ShipmentStatus.Cancelled],
        [ShipmentStatus.Delivered] = [],
        [ShipmentStatus.Cancelled] = []
    };

    public static bool CanTransition(ShipmentStatus from, ShipmentStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyCollection<ShipmentStatus> NextStates(ShipmentStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    public static bool IsTerminal(ShipmentStatus status) =>
        status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled;
}
