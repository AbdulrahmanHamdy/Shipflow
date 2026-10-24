using FluentValidation;
using ShipFlow.Domain.Tracking;

namespace ShipFlow.Application.Features.Tracking;

public sealed class SubmitTrackingPointCommandValidator : AbstractValidator<SubmitTrackingPointCommand>
{
    public SubmitTrackingPointCommandValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.RecordedAtUtc).Must(d => d.Kind == DateTimeKind.Utc).WithMessage("RecordedAtUtc must be UTC.");
        RuleFor(x => x.SpeedKph).GreaterThanOrEqualTo(0).When(x => x.SpeedKph.HasValue);
    }
}

public sealed class DeliverShipmentCommandValidator : AbstractValidator<DeliverShipmentCommand>
{
    public DeliverShipmentCommandValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(DeliveryProof.MaxRecipientLength);
        RuleFor(x => x.DeliveredAtUtc).Must(d => d.Kind == DateTimeKind.Utc).WithMessage("DeliveredAtUtc must be UTC.");
        RuleFor(x => x.Notes).MaximumLength(DeliveryProof.MaxNotesLength).When(x => x.Notes != null);
        RuleFor(x => x.SignatureReference).MaximumLength(DeliveryProof.MaxReferenceLength).When(x => x.SignatureReference != null);
        RuleFor(x => x.PhotoReference).MaximumLength(DeliveryProof.MaxReferenceLength).When(x => x.PhotoReference != null);
    }
}

public sealed class FailDeliveryCommandValidator : AbstractValidator<FailDeliveryCommand>
{
    public FailDeliveryCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class TrackingHistoryQueryValidator : AbstractValidator<TrackingHistoryQuery>
{
    public TrackingHistoryQueryValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.FromUtc).Must(d => !d.HasValue || d.Value.Kind == DateTimeKind.Utc).WithMessage("FromUtc must be UTC.");
        RuleFor(x => x.ToUtc).Must(d => !d.HasValue || d.Value.Kind == DateTimeKind.Utc).WithMessage("ToUtc must be UTC.");
    }
}
