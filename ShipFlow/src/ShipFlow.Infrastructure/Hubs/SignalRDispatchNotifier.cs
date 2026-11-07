using Microsoft.AspNetCore.SignalR;
using ShipFlow.Application.Common.Interfaces;

namespace ShipFlow.Infrastructure.Hubs;

public sealed class SignalRDispatchNotifier : IDispatchNotifier
{
    private readonly IHubContext<DispatchHub> _hub;

    public SignalRDispatchNotifier(IHubContext<DispatchHub> hub)
    {
        _hub = hub;
    }

    public async Task NotifyNewShipmentAvailableAsync(
        Guid companyId,
        Guid shipmentId,
        string reference,
        string cargoDescription,
        decimal weightKg,
        decimal volumeM3)
    {
        var payload = new
        {
            shipmentId,
            reference,
            cargoDescription,
            weightKg,
            volumeM3,
            timestampUtc = DateTime.UtcNow
        };

        await _hub.Clients.Group($"Company_{companyId}_Drivers").SendAsync("NewEligibleShipmentAvailable", payload);
        await _hub.Clients.Group($"Company_{companyId}").SendAsync("NewEligibleShipmentAvailable", payload);
        await _hub.Clients.Group("Drivers").SendAsync("NewEligibleShipmentAvailable", payload);
    }

    public async Task NotifyShipmentTakenAsync(
        Guid companyId,
        Guid shipmentId,
        Guid winningDriverId)
    {
        var payload = new
        {
            shipmentId,
            winningDriverId,
            timestampUtc = DateTime.UtcNow
        };

        await _hub.Clients.Group($"Company_{companyId}_Drivers").SendAsync("ShipmentTaken", payload);
        await _hub.Clients.Group($"Company_{companyId}").SendAsync("ShipmentTaken", payload);
        await _hub.Clients.Group("Drivers").SendAsync("ShipmentTaken", payload);
    }

    public async Task NotifyShipmentCancelledAsync(
        Guid companyId,
        Guid shipmentId)
    {
        var payload = new
        {
            shipmentId,
            timestampUtc = DateTime.UtcNow
        };

        await _hub.Clients.Group($"Company_{companyId}_Drivers").SendAsync("ShipmentCancelled", payload);
        await _hub.Clients.Group($"Company_{companyId}").SendAsync("ShipmentCancelled", payload);
        await _hub.Clients.Group("Drivers").SendAsync("ShipmentCancelled", payload);
    }

    public async Task NotifyShipmentAssignedAsync(
        Guid companyId,
        Guid customerId,
        Guid shipmentId,
        string driverName,
        string vehiclePlate)
    {
        var payload = new
        {
            shipmentId,
            driverName,
            vehiclePlate,
            timestampUtc = DateTime.UtcNow
        };

        await _hub.Clients.Group($"Company_{companyId}_Dispatchers").SendAsync("ShipmentAssigned", payload);
        await _hub.Clients.Group($"User_{customerId}").SendAsync("ShipmentAssigned", payload);
        await _hub.Clients.Group($"Company_{companyId}").SendAsync("ShipmentAssigned", payload);
    }

    public async Task NotifyDriverAvailabilityChangedAsync(
        Guid companyId,
        Guid driverId,
        bool isAvailable)
    {
        var payload = new
        {
            driverId,
            isAvailable,
            timestampUtc = DateTime.UtcNow
        };

        await _hub.Clients.Group($"Company_{companyId}_Dispatchers").SendAsync("DriverAvailabilityChanged", payload);
        await _hub.Clients.Group($"Company_{companyId}").SendAsync("DriverAvailabilityChanged", payload);
    }
}
