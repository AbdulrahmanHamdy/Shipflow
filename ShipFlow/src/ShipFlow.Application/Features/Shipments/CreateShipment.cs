using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Features.Shipments;

public sealed record StopInput(string Address, double Latitude, double Longitude, DateTime? WindowStartUtc, DateTime? WindowEndUtc);

/// <summary>The owner is always the authenticated customer; there is no customer id in the request.</summary>
public sealed record CreateShipmentCommand(string? Reference, CargoDto Cargo, IReadOnlyList<StopInput> Stops);

public sealed class CreateShipmentCommandValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentCommandValidator()
    {
        RuleFor(x => x.Reference).MaximumLength(Shipment.MaxReferenceLength);
        RuleFor(x => x.Cargo).NotNull();
        RuleFor(x => x.Cargo.Description).NotEmpty().MaximumLength(Cargo.MaxDescriptionLength).When(x => x.Cargo is not null);
        RuleFor(x => x.Cargo.WeightKg).GreaterThan(0m).When(x => x.Cargo is not null);
        RuleFor(x => x.Cargo.VolumeM3).GreaterThanOrEqualTo(0m).When(x => x.Cargo is not null);
        RuleFor(x => x.Stops).NotNull()
            .Must(s => s is null || (s.Count >= Shipment.MinStops && s.Count <= Shipment.MaxStops))
            .WithMessage($"A shipment needs between {Shipment.MinStops} and {Shipment.MaxStops} stops.");
        RuleForEach(x => x.Stops).SetValidator(new StopInputValidator());
        RuleFor(x => x.Stops)
            .Must(s => s is null || s.Count == 0 || (s[0].WindowStartUtc is not null && s[0].WindowEndUtc is not null))
            .WithMessage("The pickup stop must have a time window.");
    }
}

public sealed class StopInputValidator : AbstractValidator<StopInput>
{
    public StopInputValidator()
    {
        RuleFor(x => x.Address).NotEmpty().MaximumLength(ShipmentStop.MaxAddressLength);
        RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
        RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
        RuleFor(x => x)
            .Must(x => (x.WindowStartUtc is null) == (x.WindowEndUtc is null))
            .WithMessage("A time window needs both a start and an end.");
        RuleFor(x => x)
            .Must(x => x.WindowStartUtc is null || x.WindowEndUtc is null
                || (x.WindowStartUtc.Value.Kind == DateTimeKind.Utc && x.WindowEndUtc.Value.Kind == DateTimeKind.Utc
                    && x.WindowEndUtc > x.WindowStartUtc))
            .WithMessage("Window times must be UTC (end with 'Z') and the end must be after the start.");
    }
}

public sealed class CreateShipmentHandler
{
    private readonly IValidator<CreateShipmentCommand> _validator;
    private readonly ICurrentUser _user;
    private readonly IShipmentStore _shipments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public CreateShipmentHandler(
        IValidator<CreateShipmentCommand> validator, ICurrentUser user, IShipmentStore shipments,
        IUnitOfWork unitOfWork, TimeProvider time)
    {
        _validator = validator;
        _user = user;
        _shipments = shipments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ShipmentDetailsDto> HandleAsync(CreateShipmentCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var customerId = _user.UserId ?? throw new ForbiddenException("Authentication is required.");

        var stops = command.Stops
            .Select(s => new StopInfo(
                s.Address,
                GeoPoint.Of(s.Latitude, s.Longitude),
                s.WindowStartUtc is { } start && s.WindowEndUtc is { } end ? TimeWindow.Of(start, end) : null))
            .ToList();

        var shipment = Shipment.Create(
            customerId,
            Cargo.Of(command.Cargo.Description, command.Cargo.WeightKg, command.Cargo.VolumeM3),
            stops,
            _time.GetUtcNow().UtcDateTime,
            command.Reference);

        await _shipments.AddAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return shipment.ToDetails(new Viewer(customerId, false, null, null));
    }
}
