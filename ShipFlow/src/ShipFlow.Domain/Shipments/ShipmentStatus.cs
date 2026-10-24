namespace ShipFlow.Domain.Shipments;

public enum ShipmentStatus
{
    Pending = 0,
    Quoting = 1,
    Accepted = 2,
    Assigned = 3,
    PickedUp = 4,
    InTransit = 5,
    DeliveryFailed = 6,
    Delivered = 7,
    Cancelled = 8
}
