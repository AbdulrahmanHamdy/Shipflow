using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Features.Dispatching;

namespace ShipFlow.Application.Features.Tracking;

public sealed class GetCurrentDriverAssignmentHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IDriverAssignmentStore _driverAssignments;
    private readonly IShipmentStore _shipments;

    public GetCurrentDriverAssignmentHandler(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IDriverAssignmentStore driverAssignments,
        IShipmentStore shipments)
    {
        _currentUser = currentUser;
        _companyAccess = companyAccess;
        _fleet = fleet;
        _driverAssignments = driverAssignments;
        _shipments = shipments;
    }

    public async Task<AssignmentDto> HandleAsync(CancellationToken cancellationToken)
    {
        var driver = await DriverAccess.RequireDriverProfileAsync(
            _currentUser, _companyAccess, _fleet, cancellationToken);

        var assignment = await _driverAssignments.FindCurrentForDriverAsync(driver.Id, cancellationToken)
            ?? throw new NotFoundException("No active assignment found for the driver.");

        var shipment = await _shipments.FindAsync(assignment.ShipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        return assignment.ToDto(shipment.Status);
    }
}

public sealed class GetDriverAssignmentHistoryHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IDriverAssignmentStore _driverAssignments;

    public GetDriverAssignmentHistoryHandler(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IDriverAssignmentStore driverAssignments)
    {
        _currentUser = currentUser;
        _companyAccess = companyAccess;
        _fleet = fleet;
        _driverAssignments = driverAssignments;
    }

    public async Task<PagedResult<AssignmentDto>> HandleAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var driver = await DriverAccess.RequireDriverProfileAsync(
            _currentUser, _companyAccess, _fleet, cancellationToken);

        return await _driverAssignments.ListForDriverAsync(driver.Id, page, pageSize, cancellationToken);
    }
}
