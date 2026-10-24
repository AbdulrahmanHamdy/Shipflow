using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;

namespace ShipFlow.Application.Features.Tracking;

public sealed class PickUpShipmentHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public PickUpShipmentHandler(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IShipmentStore shipments,
        IAssignmentStore assignments,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _currentUser = currentUser;
        _companyAccess = companyAccess;
        _fleet = fleet;
        _shipments = shipments;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ShipmentSummaryDto> HandleAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        var (driver, _) = await DriverAccess.RequireAssignedDriverAsync(
            _currentUser, _companyAccess, _fleet, _assignments, shipment, ShipmentStatus.Assigned, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        shipment.PickUp(driver.UserId, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToSummary();
    }
}

public sealed class StartTransitHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public StartTransitHandler(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IShipmentStore shipments,
        IAssignmentStore assignments,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _currentUser = currentUser;
        _companyAccess = companyAccess;
        _fleet = fleet;
        _shipments = shipments;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ShipmentSummaryDto> HandleAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        var (driver, _) = await DriverAccess.RequireAssignedDriverAsync(
            _currentUser, _companyAccess, _fleet, _assignments, shipment, ShipmentStatus.PickedUp, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        shipment.StartTransit(driver.UserId, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToSummary();
    }
}

public sealed class DeliverShipmentHandler
{
    private readonly IValidator<DeliverShipmentCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly ITrackingStore _tracking;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public DeliverShipmentHandler(
        IValidator<DeliverShipmentCommand> validator,
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

    public async Task<DeliveryProofDto> HandleAsync(Guid shipmentId, DeliverShipmentCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);

        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        var existingProof = await _tracking.FindProofAsync(shipment.Id, cancellationToken);
        if (existingProof is not null)
        {
            throw new ConflictException("Delivery proof has already been captured for this shipment.");
        }

        var (driver, assignment) = await DriverAccess.RequireAssignedDriverAsync(
            _currentUser, _companyAccess, _fleet, _assignments, shipment, ShipmentStatus.InTransit, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;

        var proof = DeliveryProof.Create(
            shipment,
            driver.UserId,
            command.RecipientName,
            command.DeliveredAtUtc,
            now,
            notes: command.Notes,
            signatureReference: command.SignatureReference,
            photoReference: command.PhotoReference);

        shipment.Deliver(driver.UserId, now);
        assignment.Complete(shipment, now);

        await _tracking.AddProofAsync(proof, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return proof.ToDto();
    }
}

public sealed class FailDeliveryHandler
{
    private readonly IValidator<FailDeliveryCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public FailDeliveryHandler(
        IValidator<FailDeliveryCommand> validator,
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IShipmentStore shipments,
        IAssignmentStore assignments,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _validator = validator;
        _currentUser = currentUser;
        _companyAccess = companyAccess;
        _fleet = fleet;
        _shipments = shipments;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ShipmentSummaryDto> HandleAsync(Guid shipmentId, FailDeliveryCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);

        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        var (driver, _) = await DriverAccess.RequireAssignedDriverAsync(
            _currentUser, _companyAccess, _fleet, _assignments, shipment, ShipmentStatus.InTransit, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        shipment.FailDelivery(driver.UserId, command.Reason, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToSummary();
    }
}

public sealed class RetryDeliveryHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyAccess _companyAccess;
    private readonly IFleetStore _fleet;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public RetryDeliveryHandler(
        ICurrentUser currentUser,
        ICompanyAccess companyAccess,
        IFleetStore fleet,
        IShipmentStore shipments,
        IAssignmentStore assignments,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _currentUser = currentUser;
        _companyAccess = companyAccess;
        _fleet = fleet;
        _shipments = shipments;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ShipmentSummaryDto> HandleAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        var (driver, _) = await DriverAccess.RequireAssignedDriverAsync(
            _currentUser, _companyAccess, _fleet, _assignments, shipment, ShipmentStatus.DeliveryFailed, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        shipment.RetryDelivery(driver.UserId, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToSummary();
    }
}

public sealed class GetDeliveryProofHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly ITrackingStore _tracking;

    public GetDeliveryProofHandler(ViewerResolver viewers, IShipmentStore shipments, ITrackingStore tracking)
    {
        _viewers = viewers;
        _shipments = shipments;
        _tracking = tracking;
    }

    public async Task<DeliveryProofDto> HandleAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        var shipment = await _shipments.FindAsync(shipmentId, cancellationToken);
        if (shipment is null || !viewer.CanView(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        var proof = await _tracking.FindProofAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException("Delivery proof not found.");

        return proof.ToDto();
    }
}
