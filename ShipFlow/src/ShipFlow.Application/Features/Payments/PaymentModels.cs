using FluentValidation;

namespace ShipFlow.Application.Features.Payments;

public sealed record InitiatePaymentCommand(Guid ShipmentId);

public sealed class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
    }
}

public sealed record PaymentResponseDto(
    Guid ShipmentId,
    string OrderId,
    string PaymentKey,
    string IframeUrl,
    decimal Amount,
    string Currency);

public sealed record PaymentCallbackResponse(bool Success, string Message);
