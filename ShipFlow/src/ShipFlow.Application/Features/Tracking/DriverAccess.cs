using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Features.Tracking;

internal static class DriverAccess
{
    public static async Task<Driver> RequireAssignedDriverAsync(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IAssignmentStore assignments,
        Shipment shipment,
        CancellationToken cancellationToken)
    {
        var driver = await RequireDriverProfileAsync(currentUser, companyAccess, fleet, cancellationToken);

        if (shipment.CarrierCompanyId != driver.CompanyId)
        {
            throw new NotFoundException("Shipment not found.");
        }

        var assignment = await assignments.FindActiveForShipmentAsync(shipment.Id, cancellationToken);
        if (assignment is null || assignment.DriverId != driver.Id || assignment.Status != AssignmentStatus.Active)
        {
            throw new ForbiddenException("You are not the assigned driver for this shipment.");
        }

        return driver;
    }

    public static async Task<(Driver Driver, Assignment Assignment)> RequireAssignedDriverAsync(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IAssignmentStore assignments,
        Shipment shipment,
        ShipmentStatus requiredStatus,
        CancellationToken cancellationToken)
    {
        var driver = await RequireDriverProfileAsync(currentUser, companyAccess, fleet, cancellationToken);

        if (shipment.CarrierCompanyId != driver.CompanyId)
        {
            throw new NotFoundException("Shipment not found.");
        }

        if (shipment.Status != requiredStatus)
        {
            throw new ConflictException(
                $"Shipment cannot transition from status '{shipment.Status}'. Expected: '{requiredStatus}'.");
        }

        var assignment = await assignments.FindActiveForShipmentAsync(shipment.Id, cancellationToken);
        if (assignment is null || assignment.DriverId != driver.Id || assignment.Status != AssignmentStatus.Active)
        {
            throw new ForbiddenException("You are not the assigned driver for this shipment.");
        }

        return (driver, assignment);
    }

    public static async Task<Driver> RequireDriverProfileAsync(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        var membership = await companyAccess.FindActiveMembershipAsync(userId, cancellationToken);
        if (membership is null || membership.Role != CompanyRole.Driver)
        {
            throw new ForbiddenException("Only active drivers of the transport company can perform this action.");
        }

        var driver = await fleet.FindDriverByUserAsync(membership.CompanyId, userId, cancellationToken);
        if (driver is null || !driver.IsActive)
        {
            throw new ForbiddenException("Active driver profile not found.");
        }

        return driver;
    }
}
