using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Shipments;

namespace ShipFlow.Application.Common.Interfaces;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    // New methods
    Task<IReadOnlyList<EligibleDriverDto>> GetEligibleDriversAsync(Guid shipmentId, CancellationToken ct);
    Task<bool> TryAssignDriverAsync(Guid shipmentId, Guid driverId, Guid matchingSessionId, Viewer viewer, CancellationToken ct);
}

{
    // Retrieves the list of drivers eligible for the specified shipment.
    // Returns the same EligibleDriverDto used elsewhere.
    Task<IReadOnlyList<EligibleDriverDto>> GetEligibleDriversAsync(Guid shipmentId, Viewer viewer, CancellationToken ct);

    // Attempts to assign the given driver to the shipment within the context of a matching session.
    // Returns true if assignment succeeded, false otherwise (e.g., driver became unavailable).
    Task<bool> TryAssignDriverAsync(Guid shipmentId, Guid driverId, Guid matchingSessionId, Viewer viewer, CancellationToken ct);
    Task<MatchingStatusDto> GetMatchingStatusAsync(Guid shipmentId, Viewer viewer, CancellationToken ct);
    Task<MatchingStatusDto> DispatchForMatchingAsync(Guid shipmentId, Viewer viewer, CancellationToken ct);
    Task<MatchingStatusDto> CancelDispatchMatchingAsync(Guid shipmentId, Viewer viewer, CancellationToken ct);
    Task<IReadOnlyList<AvailableShipmentDto>> GetAvailableShipmentsForDriverAsync(Guid driverUserId, CancellationToken ct);
    Task<AssignmentDto> AcceptShipmentAsync(Guid driverUserId, Guid shipmentId, CancellationToken ct);
    Task<DriverProfileDto> GetDriverProfileAsync(Guid driverUserId, CancellationToken ct);
    Task UpdateDriverAvailabilityAsync(Guid driverUserId, bool isAvailable, CancellationToken ct);
    Task AssignDriverVehicleAsync(Guid companyId, Guid driverId, Guid? vehicleId, CancellationToken ct);
}
