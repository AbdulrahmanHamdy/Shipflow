using FluentValidation;
using Microsoft.Extensions.Logging;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Features.Payments;

public sealed class InitiatePaymentHandler
{
    private readonly IValidator<InitiatePaymentCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly IShipmentStore _shipments;
    private readonly IPaymentService _paymentService;

    public InitiatePaymentHandler(
        IValidator<InitiatePaymentCommand> validator,
        ICurrentUser currentUser,
        IShipmentStore shipments,
        IPaymentService paymentService)
    {
        _validator = validator;
        _currentUser = currentUser;
        _shipments = shipments;
        _paymentService = paymentService;
    }

    public async Task<PaymentResponseDto> HandleAsync(InitiatePaymentCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        var shipment = await _shipments.FindAsync(command.ShipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment not found.");

        if (!shipment.IsOwnedBy(userId))
        {
            throw new NotFoundException("Shipment not found.");
        }

        if (shipment.AgreedPrice is null)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, "Shipment has no agreed price to pay.");
        }

        // Amount in cents (Paymob expects integer cents)
        var amountCents = shipment.AgreedPrice.Amount * 100m;
        var email = _currentUser.Email ?? "customer@shipflow.test";
        var name = "Customer " + userId.ToString()[..8];

        var result = await _paymentService.InitiatePaymentAsync(
            shipment.Id,
            amountCents,
            shipment.AgreedPrice.Currency,
            email,
            name,
            cancellationToken);

        return new PaymentResponseDto(
            shipment.Id,
            result.OrderId,
            result.PaymentKey,
            result.IframeUrl,
            shipment.AgreedPrice.Amount,
            shipment.AgreedPrice.Currency);
    }
}

public sealed partial class ProcessPaymentCallbackHandler
{
    private readonly IPaymentService _paymentService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ProcessPaymentCallbackHandler> _logger;

    public ProcessPaymentCallbackHandler(
        IPaymentService paymentService,
        IEmailSender emailSender,
        ILogger<ProcessPaymentCallbackHandler> logger)
    {
        _paymentService = paymentService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<PaymentCallbackResponse> HandleAsync(PaymentCallbackData data, CancellationToken cancellationToken)
    {
        var verification = _paymentService.VerifyCallback(data);
        if (!verification.IsAuthentic)
        {
            LogHmacFailed(data.TransactionId);
            throw new ForbiddenException("HMAC verification failed.");
        }

        if (verification.IsSuccess)
        {
            LogPaymentSuccess(verification.TransactionId, data.OrderId);

            // Send payment confirmation email asynchronously (swallow failure if smtp unavailable)
            try
            {
                var customerEmail = data.RawFields.TryGetValue("shipping_data.email", out var email) ? email : "customer@example.com";
                await _emailSender.SendAsync(new EmailMessage(
                    customerEmail,
                    "Customer",
                    "ShipFlow Payment Confirmation",
                    $"<p>Your payment for order {data.OrderId} of amount {data.AmountCents / 100m:F2} has been successfully processed.</p>",
                    $"Your payment for order {data.OrderId} of amount {data.AmountCents / 100m:F2} has been successfully processed."
                ), cancellationToken);
            }
            catch (Exception ex)
            {
                LogEmailFailed(ex);
            }

            return new PaymentCallbackResponse(true, "Payment verified and recorded.");
        }

        LogPaymentUnsuccessful(verification.TransactionId);
        return new PaymentCallbackResponse(false, "Payment transaction was not successful.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paymob callback HMAC verification failed for transaction {TransactionId}")]
    private partial void LogHmacFailed(string transactionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Paymob payment successful for transaction {TransactionId}, order {OrderId}")]
    private partial void LogPaymentSuccess(string transactionId, string orderId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send payment confirmation email.")]
    private partial void LogEmailFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Paymob payment not successful for transaction {TransactionId}")]
    private partial void LogPaymentUnsuccessful(string transactionId);
}
