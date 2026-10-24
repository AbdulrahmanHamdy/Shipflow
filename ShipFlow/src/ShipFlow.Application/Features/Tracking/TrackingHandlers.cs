using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Tracking;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Features.Tracking;

public sealed class SubmitTrackingPointHandler
{
    private readonly IValidator<SubmitTrackingPointCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly ITrackingStore _tracking;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public SubmitTrackingPointHandler(
        IValidator<SubmitTrackingPointCommand> validator,
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IShipmentStore shipments,
        IAssignmentStore assignments,
        ITrackingStore tracking,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _validator = validator;
        _currentUser = currentUser;
        _companyAccess = companyAccess;
        _fleet = fleet;
        _shipments = shipments;
        _assignments = assignments;
        _tracking = tracking;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<TrackingPointDto> HandleAsync(SubmitTrackingPointCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);

        var shipment = await _shipments.FindAsync(command.ShipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        var driver = await DriverAccess.RequireAssignedDriverAsync(
            _currentUser, _companyAccess, _fleet, _assignments, shipment, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;

        // Idempotency: duplicate GPS submissions with same timestamp and user are safely deduplicated
        if (await _tracking.ExistsAsync(shipment.Id, command.RecordedAtUtc, driver.UserId, cancellationToken))
        {
            var latest = await _tracking.FindLatestAsync(shipment.Id, cancellationToken);
            if (latest is not null)
            {
                return latest.ToDto();
            }
        }

        var location = GeoPoint.Of(command.Latitude, command.Longitude);
        var point = TrackingPoint.Create(shipment, driver.UserId, location, command.RecordedAtUtc, now, command.SpeedKph);

        await _tracking.AddPointAsync(point, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return point.ToDto();
    }
}

public sealed class GetLatestTrackingHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly ITrackingStore _tracking;

    public GetLatestTrackingHandler(ViewerResolver viewers, IShipmentStore shipments, ITrackingStore tracking)
    {
        _viewers = viewers;
        _shipments = shipments;
        _tracking = tracking;
    }

    public async Task<TrackingPointDto?> HandleAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        var shipment = await _shipments.FindAsync(shipmentId, cancellationToken);
        if (shipment is null || !viewer.CanView(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        var point = await _tracking.FindLatestAsync(shipmentId, cancellationToken);
        return point?.ToDto();
    }
}

public sealed class GetTrackingHistoryHandler
{
    private readonly IValidator<TrackingHistoryQuery> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly ITrackingStore _tracking;

    public GetTrackingHistoryHandler(
        IValidator<TrackingHistoryQuery> validator,
        ViewerResolver viewers,
        IShipmentStore shipments,
        ITrackingStore tracking)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
        _tracking = tracking;
    }

    public async Task<PagedResult<TrackingPointDto>> HandleAsync(TrackingHistoryQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, query, cancellationToken);

        var viewer = await _viewers.ResolveAsync(cancellationToken);
        var shipment = await _shipments.FindAsync(query.ShipmentId, cancellationToken);
        if (shipment is null || !viewer.CanView(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        var filter = new TrackingListFilter(query.ShipmentId, query.FromUtc, query.ToUtc, query.Page, query.PageSize);
        return await _tracking.ListAsync(filter, cancellationToken);
    }
}
