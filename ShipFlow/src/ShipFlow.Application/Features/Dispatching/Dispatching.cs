using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Features.Dispatching;

/// <summary>Shared window rules for availability queries and assignment: UTC, end after start, bounded length.</summary>
public static class WindowRules
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(14);

    public static bool IsValid(DateTime startUtc, DateTime endUtc) =>
        startUtc.Kind == DateTimeKind.Utc && endUtc.Kind == DateTimeKind.Utc
        && endUtc > startUtc && endUtc - startUtc <= MaxDuration;

    public const string Message = "The window times must be UTC (end with 'Z'), the end must be after the start, and the window at most 14 days.";
}

public sealed record AvailabilityQuery(DateTime WindowStartUtc, DateTime WindowEndUtc);

public sealed class AvailabilityQueryValidator : AbstractValidator<AvailabilityQuery>
{
    public AvailabilityQueryValidator() =>
        RuleFor(x => x).Must(x => WindowRules.IsValid(x.WindowStartUtc, x.WindowEndUtc)).WithMessage(WindowRules.Message);
}

/// <summary>Driver, vehicle and window only. The company, the assigning user and every status are derived server-side.</summary>
public sealed record AssignShipmentCommand(Guid DriverId, Guid VehicleId, DateTime WindowStartUtc, DateTime WindowEndUtc);

public sealed class AssignShipmentCommandValidator : AbstractValidator<AssignShipmentCommand>
{
    public AssignShipmentCommandValidator()
    {
        RuleFor(x => x.DriverId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x).Must(x => WindowRules.IsValid(x.WindowStartUtc, x.WindowEndUtc)).WithMessage(WindowRules.Message);
    }
}

public sealed record UnassignShipmentCommand(string Reason);

public sealed class UnassignShipmentCommandValidator : AbstractValidator<UnassignShipmentCommand>
{
    public UnassignShipmentCommandValidator() =>
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Assignment.MaxReasonLength);
}

/// <summary>Resolves the shipment for dispatch work: the caller must be dispatch staff of the shipment's carrier company.</summary>
internal static class DispatchAccess
{
    public static Viewer RequireDispatcher(Viewer viewer)
    {
        if (!viewer.CanDispatch)
        {
            throw new ForbiddenException("Only an administrator or dispatcher of a transport company can dispatch shipments.");
        }

        return viewer;
    }

    /// <summary>Another company's shipment (or one with no carrier yet) is reported as not found.</summary>
    public static Shipment RequireCarrierShipment(Shipment? shipment, Viewer viewer) =>
        shipment is not null && viewer.IsCarrierOf(shipment) ? shipment : throw new NotFoundException("Shipment not found.");
}

public sealed class GetAvailableVehiclesHandler
{
    private readonly IValidator<AvailabilityQuery> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IFleetStore _fleet;
    private readonly IAssignmentStore _assignments;

    public GetAvailableVehiclesHandler(
        IValidator<AvailabilityQuery> validator, ViewerResolver viewers, IShipmentStore shipments,
        IFleetStore fleet, IAssignmentStore assignments)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
        _fleet = fleet;
        _assignments = assignments;
    }

    /// <summary>
    /// Vehicles of the shipment's carrier that are active, big enough (<c>Capacity.CanCarry</c>) and not booked in the window.
    /// This is advice for the dispatcher; the authoritative check happens again, under lock, when assigning.
    /// </summary>
    public async Task<IReadOnlyList<VehicleDto>> HandleAsync(Guid shipmentId, AvailabilityQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, query, cancellationToken);
        var viewer = DispatchAccess.RequireDispatcher(await _viewers.ResolveAsync(cancellationToken));
        var shipment = DispatchAccess.RequireCarrierShipment(await _shipments.FindAsync(shipmentId, cancellationToken), viewer);
        EnsureAssignable(shipment);

        var window = TimeWindow.Of(query.WindowStartUtc, query.WindowEndUtc);
        var busy = (await _assignments.ListActiveOverlappingAsync(viewer.CompanyId!.Value, window, cancellationToken))
            .Select(a => a.VehicleId)
            .ToHashSet();

        var vehicles = await _fleet.ListActiveVehiclesAsync(viewer.CompanyId!.Value, cancellationToken);
        return vehicles
            .Where(v => !busy.Contains(v.Id) && v.Capacity.CanCarry(shipment.Cargo))
            .OrderBy(v => v.PlateNumber, StringComparer.Ordinal)
            .Select(v => v.ToDto())
            .ToList();
    }

    internal static void EnsureAssignable(Shipment shipment)
    {
        if (shipment.Status != ShipmentStatus.Accepted)
        {
            throw new DomainException(
                DomainErrorCodes.InvalidState,
                $"Only an Accepted shipment can be assigned (current: {shipment.Status}).");
        }
    }
}

public sealed class GetAvailableDriversHandler
{
    private readonly IValidator<AvailabilityQuery> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IFleetStore _fleet;
    private readonly IAssignmentStore _assignments;

    public GetAvailableDriversHandler(
        IValidator<AvailabilityQuery> validator, ViewerResolver viewers, IShipmentStore shipments,
        IFleetStore fleet, IAssignmentStore assignments)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
        _fleet = fleet;
        _assignments = assignments;
    }

    /// <summary>Active drivers of the shipment's carrier that are not booked in the window.</summary>
    public async Task<IReadOnlyList<DriverDto>> HandleAsync(Guid shipmentId, AvailabilityQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, query, cancellationToken);
        var viewer = DispatchAccess.RequireDispatcher(await _viewers.ResolveAsync(cancellationToken));
        var shipment = DispatchAccess.RequireCarrierShipment(await _shipments.FindAsync(shipmentId, cancellationToken), viewer);
        GetAvailableVehiclesHandler.EnsureAssignable(shipment);

        var window = TimeWindow.Of(query.WindowStartUtc, query.WindowEndUtc);
        var busy = (await _assignments.ListActiveOverlappingAsync(viewer.CompanyId!.Value, window, cancellationToken))
            .Select(a => a.DriverId)
            .ToHashSet();

        var drivers = await _fleet.ListActiveDriversAsync(viewer.CompanyId!.Value, cancellationToken);
        return drivers
            .Where(d => !busy.Contains(d.Id))
            .OrderBy(d => d.FullName, StringComparer.Ordinal)
            .Select(d => d.ToDto())
            .ToList();
    }
}

public sealed class AssignShipmentHandler
{
    private readonly IValidator<AssignShipmentCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IFleetStore _fleet;
    private readonly IAssignmentStore _assignments;
    private readonly IFleetTransaction _transaction;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public AssignShipmentHandler(
        IValidator<AssignShipmentCommand> validator, ViewerResolver viewers, IShipmentStore shipments, IFleetStore fleet,
        IAssignmentStore assignments, IFleetTransaction transaction, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
        _fleet = fleet;
        _assignments = assignments;
        _transaction = transaction;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <summary>
    /// Books a driver and a vehicle of the carrier company for the shipment.
    /// <list type="number">
    /// <item>Cheap pre-checks outside the transaction: the shipment is the caller's company's, the driver and vehicle belong to
    /// that company (otherwise 404, and no lock is ever taken on another tenant's rows).</item>
    /// <item>Inside <see cref="IFleetTransaction"/> (exclusive lock on the driver and the vehicle): everything is read again,
    /// the overlapping active assignments are loaded and <c>Assignment.Create</c> decides. Because the locks are held until
    /// commit, two competing requests cannot both pass the overlap check: the second one waits, then sees the first one's
    /// committed assignment and gets <c>assignment_conflict</c> (409).</item>
    /// </list>
    /// </summary>
    public async Task<AssignmentDto> HandleAsync(Guid shipmentId, AssignShipmentCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = DispatchAccess.RequireDispatcher(await _viewers.ResolveAsync(cancellationToken));
        var companyId = viewer.CompanyId!.Value;

        DispatchAccess.RequireCarrierShipment(await _shipments.FindAsync(shipmentId, cancellationToken), viewer);
        if (await _fleet.FindDriverAsync(companyId, command.DriverId, cancellationToken) is null)
        {
            throw new NotFoundException("Driver not found.");
        }

        if (await _fleet.FindVehicleAsync(companyId, command.VehicleId, cancellationToken) is null)
        {
            throw new NotFoundException("Vehicle not found.");
        }

        var window = TimeWindow.Of(command.WindowStartUtc, command.WindowEndUtc);
        var locks = new FleetLocks([command.DriverId], [command.VehicleId], []);

        return await _transaction.RunAsync(locks, async ct =>
        {
            var now = _time.GetUtcNow().UtcDateTime;

            // Everything below is read after the locks were taken, so it reflects every commit made before us.
            var shipment = DispatchAccess.RequireCarrierShipment(await _shipments.FindForUpdateAsync(shipmentId, ct), viewer);
            var driver = await _fleet.FindDriverAsync(companyId, command.DriverId, ct) ?? throw new NotFoundException("Driver not found.");
            var vehicle = await _fleet.FindVehicleAsync(companyId, command.VehicleId, ct) ?? throw new NotFoundException("Vehicle not found.");
            var existing = await _assignments.ListActiveOverlappingForAsync(driver.Id, vehicle.Id, window, ct);

            // State, same company, active driver/vehicle, capacity, future window and overlap are all decided by the domain.
            var assignment = Assignment.Create(shipment, driver, vehicle, window, viewer.UserId, existing, now);

            await _assignments.AddAsync(assignment, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return assignment.ToDto(shipment.Status);
        }, cancellationToken);
    }
}

public sealed class UnassignShipmentHandler
{
    private readonly IValidator<UnassignShipmentCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public UnassignShipmentHandler(
        IValidator<UnassignShipmentCommand> validator, ViewerResolver viewers, IShipmentStore shipments,
        IAssignmentStore assignments, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <summary>
    /// Releases the shipment's active assignment and moves it Assigned to Accepted so it can be assigned again. Only possible
    /// while the shipment is Assigned (the domain rejects it once pickup has happened). Freeing resources needs no lock;
    /// racing a cancel or a re-assign is settled by the shipment's rowversion.
    /// </summary>
    public async Task<ShipmentDetailsDto> HandleAsync(Guid shipmentId, UnassignShipmentCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = DispatchAccess.RequireDispatcher(await _viewers.ResolveAsync(cancellationToken));
        var shipment = DispatchAccess.RequireCarrierShipment(await _shipments.FindForUpdateAsync(shipmentId, cancellationToken), viewer);
        var assignment = await _assignments.FindActiveForShipmentAsync(shipment.Id, cancellationToken)
            ?? throw new NotFoundException("The shipment has no active assignment.");

        assignment.UnassignFrom(shipment, viewer.UserId, command.Reason, _time.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToDetails(viewer);
    }
}

public sealed class GetShipmentAssignmentHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;

    public GetShipmentAssignmentHandler(ViewerResolver viewers, IShipmentStore shipments, IAssignmentStore assignments)
    {
        _viewers = viewers;
        _shipments = shipments;
        _assignments = assignments;
    }

    /// <summary>The shipment's active assignment, for its carrier company's dispatch staff.</summary>
    public async Task<AssignmentDto> HandleAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var viewer = DispatchAccess.RequireDispatcher(await _viewers.ResolveAsync(cancellationToken));
        var shipment = DispatchAccess.RequireCarrierShipment(await _shipments.FindAsync(shipmentId, cancellationToken), viewer);
        var assignment = await _assignments.FindActiveForShipmentAsync(shipment.Id, cancellationToken)
            ?? throw new NotFoundException("The shipment has no active assignment.");
        return assignment.ToDto(shipment.Status);
    }
}
