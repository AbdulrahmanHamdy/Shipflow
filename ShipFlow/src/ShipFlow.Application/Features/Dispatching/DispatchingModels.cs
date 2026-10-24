using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Features.Dispatching;

public sealed record AssignmentDto(
    Guid Id, Guid ShipmentId, Guid CompanyId, Guid DriverId, Guid VehicleId,
    DateTime WindowStartUtc, DateTime WindowEndUtc, string Status, string ShipmentStatus,
    Guid AssignedByUserId, DateTime CreatedAtUtc, DateTime? EndedAtUtc, string? ReleaseReason);

public static class DispatchingMapping
{
    public static AssignmentDto ToDto(this Assignment a, ShipmentStatus shipmentStatus) =>
        new(a.Id, a.ShipmentId, a.CompanyId, a.DriverId, a.VehicleId, a.Window.StartUtc, a.Window.EndUtc,
            a.Status.ToString(), shipmentStatus.ToString(), a.AssignedByUserId, a.CreatedAtUtc, a.EndedAtUtc, a.ReleaseReason);
}
