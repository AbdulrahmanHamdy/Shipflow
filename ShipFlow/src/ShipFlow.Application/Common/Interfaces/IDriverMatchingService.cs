using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Shipments;

namespace ShipFlow.Application.Common.Interfaces;

public interface IDriverMatchingService
{
    Task<MatchingStatusDto> GetMatchingStatusAsync(Guid shipmentId, Viewer viewer, CancellationToken ct);
    Task<MatchingStatusDto> DispatchForMatchingAsync(Guid shipmentId, Viewer viewer, CancellationToken ct);
    Task<MatchingStatusDto> CancelDispatchMatchingAsync(Guid shipmentId, Viewer viewer, CancellationToken ct);
    Task<IReadOnlyList<AvailableShipmentDto>> GetAvailableShipmentsForDriverAsync(Guid driverUserId, CancellationToken ct);
    Task<AssignmentDto> AcceptShipmentAsync(Guid driverUserId, Guid shipmentId, CancellationToken ct);
    Task<DriverProfileDto> GetDriverProfileAsync(Guid driverUserId, CancellationToken ct);
    Task UpdateDriverAvailabilityAsync(Guid driverUserId, bool isAvailable, CancellationToken ct);
    Task AssignDriverVehicleAsync(Guid companyId, Guid driverId, Guid? vehicleId, CancellationToken ct);
}
