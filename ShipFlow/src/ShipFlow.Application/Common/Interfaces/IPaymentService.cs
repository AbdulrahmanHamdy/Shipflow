namespace ShipFlow.Application.Common.Interfaces;

/// <summary>Result returned to the client to launch the payment iframe.</summary>
public sealed record PaymentInitiationResult(string PaymentKey, string IframeUrl, string OrderId);

/// <summary>Data parsed from a Paymob callback (HMAC-protected).</summary>
public sealed record PaymentCallbackData(
    string TransactionId,
    string OrderId,
    bool IsSuccess,
    decimal AmountCents,
    string Hmac,
    IDictionary<string, string> RawFields);

/// <summary>Result of server-side HMAC verification of a callback.</summary>
public sealed record PaymentVerificationResult(bool IsAuthentic, bool IsSuccess, string TransactionId);

/// <summary>
/// Paymob payment gateway. Credentials live in Infrastructure configuration (never in Application code).
/// Never trust client-reported amounts or statuses; always verify server-side.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Initiates a payment via the Paymob 3-step API (auth → order → payment key).
    /// Returns the iframe URL the client should open.
    /// </summary>
    Task<PaymentInitiationResult> InitiatePaymentAsync(
        Guid shipmentId,
        decimal amountCents,
        string currency,
        string customerEmail,
        string customerName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a Paymob callback using HMAC-SHA512 computed from sorted query-string fields.
    /// Returns the verification result. Never throws on a bad HMAC — returns IsAuthentic=false.
    /// </summary>
    PaymentVerificationResult VerifyCallback(PaymentCallbackData data);
}
