namespace ShipFlow.Application.Common.Interfaces;

public interface IDispatchNotifier
{
    Task NotifyNewShipmentAvailableAsync(
        Guid companyId,
        Guid shipmentId,
        string reference,
        string cargoDescription,
        decimal weightKg,
        decimal volumeM3);

    Task NotifyShipmentTakenAsync(
        Guid companyId,
        Guid shipmentId,
        Guid winningDriverId);

    Task NotifyShipmentCancelledAsync(
        Guid companyId,
        Guid shipmentId);

    Task NotifyShipmentAssignedAsync(
        Guid companyId,
        Guid customerId,
        Guid shipmentId,
        string driverName,
        string vehiclePlate);

    Task NotifyDriverAvailabilityChangedAsync(
        Guid companyId,
        Guid driverId,
        bool isAvailable);
}
