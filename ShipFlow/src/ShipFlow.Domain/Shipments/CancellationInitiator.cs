namespace ShipFlow.Domain.Shipments;

/// <summary>Who is cancelling a shipment; determines the identity check and the states it is allowed from.</summary>
public enum CancellationInitiator
{
    Customer = 0,
    Carrier = 1,
    Platform = 2
}
