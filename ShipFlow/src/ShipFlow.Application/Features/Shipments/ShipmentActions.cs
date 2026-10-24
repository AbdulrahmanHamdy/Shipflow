using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Features.Shipments;

public sealed class GetShipmentByIdHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;

    public GetShipmentByIdHandler(ViewerResolver viewers, IShipmentStore shipments)
    {
        _viewers = viewers;
        _shipments = shipments;
    }

    public async Task<ShipmentDetailsDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        var shipment = await _shipments.FindAsync(id, cancellationToken);
        if (shipment is null || !viewer.CanView(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        return shipment.ToDetails(viewer);
    }
}

public enum ShipmentScope
{
    /// <summary>Default. Customers: their own shipments. Companies: shipments they carry. Admins: all.</summary>
    Mine = 0,

    /// <summary>Company staff only: shipments currently open for quotes.</summary>
    OpenForQuotes = 1
}

public sealed record GetShipmentsQuery(
    ShipmentScope Scope = ShipmentScope.Mine,
    List<ShipmentStatus>? Statuses = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed class GetShipmentsQueryValidator : AbstractValidator<GetShipmentsQuery>
{
    public const int MaxPageSize = 100;

    public GetShipmentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(Shipment.MaxReferenceLength);
        RuleFor(x => x)
            .Must(x => x.CreatedFromUtc is null || x.CreatedToUtc is null || x.CreatedFromUtc <= x.CreatedToUtc)
            .WithMessage("createdFromUtc must not be after createdToUtc.");
    }
}

public sealed class GetShipmentsHandler
{
    private readonly IValidator<GetShipmentsQuery> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;

    public GetShipmentsHandler(IValidator<GetShipmentsQuery> validator, ViewerResolver viewers, IShipmentStore shipments)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
    }

    public async Task<Common.Models.PagedResult<ShipmentSummaryDto>> HandleAsync(GetShipmentsQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, query, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);

        // The visibility scope is decided here from the caller's identity; the query can only narrow it.
        Guid? customerId = null;
        Guid? carrierId = null;
        var openOnly = false;

        if (query.Scope == ShipmentScope.OpenForQuotes)
        {
            if (viewer.CompanyId is null && !viewer.IsPlatformAdmin)
            {
                throw new ForbiddenException("Only transport companies can browse shipments open for quotes.");
            }

            openOnly = true;
        }
        else if (viewer.IsPlatformAdmin)
        {
            // no restriction
        }
        else if (viewer.CompanyId is { } companyId)
        {
            carrierId = companyId;
        }
        else
        {
            customerId = viewer.UserId;
        }

        var filter = new ShipmentListFilter(
            customerId, carrierId, openOnly, query.Statuses ?? [],
            query.CreatedFromUtc, query.CreatedToUtc, string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.Page, query.PageSize);

        return await _shipments.ListAsync(filter, cancellationToken);
    }
}

public sealed class OpenForQuotingHandler
{
    private readonly ICurrentUser _user;
    private readonly IShipmentStore _shipments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public OpenForQuotingHandler(ICurrentUser user, IShipmentStore shipments, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _user = user;
        _shipments = shipments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ShipmentDetailsDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Authentication is required.");
        var shipment = await _shipments.FindForUpdateAsync(id, cancellationToken);
        if (shipment is null || !shipment.IsOwnedBy(userId))
        {
            throw new NotFoundException("Shipment not found.");
        }

        shipment.OpenForQuoting(userId, _time.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToDetails(new Viewer(userId, false, null, null));
    }
}

public sealed record CancelShipmentCommand(string Reason);

public sealed class CancelShipmentCommandValidator : AbstractValidator<CancelShipmentCommand>
{
    public CancelShipmentCommandValidator() =>
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Shipment.MaxReasonLength);
}

public sealed class CancelShipmentHandler
{
    private readonly IValidator<CancelShipmentCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public CancelShipmentHandler(
        IValidator<CancelShipmentCommand> validator, ViewerResolver viewers, IShipmentStore shipments,
        IAssignmentStore assignments, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ShipmentDetailsDto> HandleAsync(Guid id, CancelShipmentCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        var shipment = await _shipments.FindForUpdateAsync(id, cancellationToken);
        if (shipment is null || !viewer.CanView(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        // Who is cancelling, and in which capacity, is derived from the caller; the domain then applies the
        // per-initiator state rules (e.g. a customer cannot cancel after pickup).
        CancellationInitiator initiator;
        Guid initiatorId;
        if (shipment.IsOwnedBy(viewer.UserId))
        {
            (initiator, initiatorId) = (CancellationInitiator.Customer, viewer.UserId);
        }
        else if (viewer.IsPlatformAdmin)
        {
            (initiator, initiatorId) = (CancellationInitiator.Platform, viewer.UserId);
        }
        else if (viewer.IsCarrierOf(shipment) && viewer.CanManageQuotes)
        {
            (initiator, initiatorId) = (CancellationInitiator.Carrier, viewer.CompanyId!.Value);
        }
        else
        {
            throw new ForbiddenException("You are not allowed to cancel this shipment.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var activeAssignment = await _assignments.FindActiveForShipmentAsync(shipment.Id, cancellationToken);

        shipment.Cancel(initiator, initiatorId, command.Reason, now);

        // Same unit of work: the driver and vehicle become free in the same commit that cancels the shipment.
        activeAssignment?.Release("Shipment cancelled: " + command.Reason.Trim(), now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToDetails(viewer);
    }
}
