using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipFlow.Application.Common.Interfaces;

namespace ShipFlow.Infrastructure.Payment;

public sealed class PaymobSettings
{
    public const string SectionName = "Paymob";

    public string BaseUrl { get; set; } = "https://accept.paymob.com";

    public string ApiKey { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public string HmacSecret { get; set; } = string.Empty;

    public string IntegrationId { get; set; } = string.Empty;

    public string IframeId { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(HmacSecret);
}

public sealed partial class PaymobService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<PaymobSettings> _settings;
    private readonly ILogger<PaymobService> _logger;

    public PaymobService(
        HttpClient httpClient,
        IOptions<PaymobSettings> settings,
        ILogger<PaymobService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<PaymentInitiationResult> InitiatePaymentAsync(
        Guid shipmentId,
        decimal amountCents,
        string currency,
        string customerEmail,
        string customerName,
        CancellationToken cancellationToken)
    {
        var settings = _settings.Value;
        if (!settings.IsConfigured)
        {
            LogMockInitiation(shipmentId);
            var mockToken = "mock_paymob_token_" + Guid.NewGuid().ToString("N");
            return new PaymentInitiationResult(
                mockToken,
                $"{settings.BaseUrl}/api/acceptance/iframes/{settings.IframeId}?payment_token={mockToken}",
                "MOCK-ORD-" + Guid.NewGuid().ToString("N")[..8]);
        }

        var baseUrl = settings.BaseUrl.TrimEnd('/');

        // Step 1: Authentication Request
        var authContent = new StringContent(
            JsonSerializer.Serialize(new { api_key = settings.ApiKey }),
            Encoding.UTF8,
            "application/json");

        var authResponse = await _httpClient.PostAsync($"{baseUrl}/api/auth/tokens", authContent, cancellationToken);
        authResponse.EnsureSuccessStatusCode();
        var authJson = await authResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
        var authToken = authJson?["token"]?.ToString()
            ?? throw new InvalidOperationException("Failed to obtain Paymob authentication token.");

        // Step 2: Order Registration
        var orderPayload = new
        {
            auth_token = authToken,
            delivery_needed = "false",
            amount_cents = ((long)amountCents).ToString(CultureInfo.InvariantCulture),
            currency,
            merchant_order_id = $"{shipmentId}_{DateTime.UtcNow.Ticks}",
            items = Array.Empty<object>()
        };

        var orderResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/ecommerce/orders", orderPayload, cancellationToken);
        orderResponse.EnsureSuccessStatusCode();
        var orderJson = await orderResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
        var orderId = orderJson?["id"]?.ToString()
            ?? throw new InvalidOperationException("Failed to register Paymob order.");

        // Step 3: Payment Key Request
        var billingData = new
        {
            first_name = customerName,
            last_name = "Customer",
            email = customerEmail,
            phone_number = "+201000000000",
            street = "NA",
            building = "NA",
            floor = "NA",
            apartment = "NA",
            city = "Cairo",
            country = "EG",
            postal_code = "12345",
            state = "Cairo"
        };

        var keyPayload = new
        {
            auth_token = authToken,
            amount_cents = ((long)amountCents).ToString(CultureInfo.InvariantCulture),
            expiration = 3600,
            order_id = orderId,
            billing_data = billingData,
            currency,
            integration_id = settings.IntegrationId
        };

        var keyResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/acceptance/payment_keys", keyPayload, cancellationToken);
        keyResponse.EnsureSuccessStatusCode();
        var keyJson = await keyResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
        var paymentKey = keyJson?["token"]?.ToString()
            ?? throw new InvalidOperationException("Failed to obtain Paymob payment key.");

        var iframeUrl = $"{baseUrl}/api/acceptance/iframes/{settings.IframeId}?payment_token={paymentKey}";

        LogInitiated(shipmentId, orderId);

        return new PaymentInitiationResult(paymentKey, iframeUrl, orderId);
    }

    public PaymentVerificationResult VerifyCallback(PaymentCallbackData data)
    {
        var settings = _settings.Value;
        if (string.IsNullOrWhiteSpace(settings.HmacSecret))
        {
            LogHmacNotConfigured();
            return new PaymentVerificationResult(false, false, data.TransactionId);
        }

        if (string.IsNullOrWhiteSpace(data.Hmac))
        {
            return new PaymentVerificationResult(false, false, data.TransactionId);
        }

        // Paymob HMAC verification:
        // Concatenates transaction fields in exact Paymob required order
        var fields = new[]
        {
            "amount_cents",
            "created_at",
            "currency",
            "error_occured",
            "has_parent_transaction",
            "id",
            "integration_id",
            "is_3d_secure",
            "is_auth",
            "is_capture",
            "is_refunded",
            "is_standalone_payment",
            "is_voided",
            "order.id",
            "owner",
            "pending",
            "source_data.pan",
            "source_data.sub_type",
            "source_data.type",
            "success"
        };

        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            if (data.RawFields.TryGetValue(field, out var val))
            {
                builder.Append(val);
            }
        }

        var concatenated = builder.ToString();
        var calculatedHmac = ComputeHmacSha512(concatenated, settings.HmacSecret);

        // Also check fallback for query-param alphabetical concatenation (Paymob redirect callbacks)
        bool isValid = string.Equals(calculatedHmac, data.Hmac, StringComparison.OrdinalIgnoreCase);

        if (!isValid)
        {
            var sortedBuilder = new StringBuilder();
            foreach (var kvp in data.RawFields.Where(k => !k.Key.Equals("hmac", StringComparison.OrdinalIgnoreCase)).OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sortedBuilder.Append(kvp.Value);
            }
            var sortedCalculated = ComputeHmacSha512(sortedBuilder.ToString(), settings.HmacSecret);
            isValid = string.Equals(sortedCalculated, data.Hmac, StringComparison.OrdinalIgnoreCase);
        }

        return new PaymentVerificationResult(isValid, isValid && data.IsSuccess, data.TransactionId);
    }

    private static string ComputeHmacSha512(string text, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var textBytes = Encoding.UTF8.GetBytes(text);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(textBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paymob is not configured. Returning simulated mock payment initiation for shipment {ShipmentId}.")]
    private partial void LogMockInitiation(Guid shipmentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully initiated Paymob payment for shipment {ShipmentId}, order {OrderId}.")]
    private partial void LogInitiated(Guid shipmentId, string orderId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paymob HMAC secret is not configured; callback verification rejected.")]
    private partial void LogHmacNotConfigured();
}
