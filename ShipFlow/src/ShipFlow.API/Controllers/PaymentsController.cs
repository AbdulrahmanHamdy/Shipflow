using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Payments;

namespace ShipFlow.API.Controllers;

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController : ControllerBase
{
    /// <summary>Initiates a Paymob payment session for a shipment with an accepted quote.</summary>
    [HttpPost("initiate")]
    [Authorize(Policy = Policies.RequireCustomer)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponseDto>> Initiate(
        [FromBody] InitiatePaymentCommand command,
        [FromServices] InitiatePaymentHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(command, ct));

    /// <summary>Webhook callback endpoint invoked by Paymob upon transaction completion.</summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentCallbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaymentCallbackResponse>> PostCallback(
        [FromBody] System.Text.Json.Nodes.JsonObject payload,
        [FromServices] ProcessPaymentCallbackHandler handler,
        CancellationToken ct)
    {
        var rawFields = new Dictionary<string, string>();
        ExtractFields(payload, rawFields, "");

        var hmac = rawFields.TryGetValue("hmac", out var h) ? h : string.Empty;
        var txnId = rawFields.TryGetValue("id", out var id) ? id : (rawFields.TryGetValue("transaction_id", out var tid) ? tid : string.Empty);
        var orderId = rawFields.TryGetValue("order.id", out var oid) ? oid : (rawFields.TryGetValue("order_id", out var orid) ? orid : string.Empty);
        var success = rawFields.TryGetValue("success", out var s) && bool.TryParse(s, out var b) && b;
        var amountCents = rawFields.TryGetValue("amount_cents", out var ac) && decimal.TryParse(ac, out var a) ? a : 0m;

        var data = new PaymentCallbackData(txnId, orderId, success, amountCents, hmac, rawFields);
        return Ok(await handler.HandleAsync(data, ct));
    }

    /// <summary>Redirect callback endpoint invoked when client finishes payment and redirects back.</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentCallbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaymentCallbackResponse>> GetCallback(
        [FromServices] ProcessPaymentCallbackHandler handler,
        CancellationToken ct)
    {
        var rawFields = new Dictionary<string, string>();
        foreach (var query in Request.Query)
        {
            rawFields[query.Key] = query.Value.ToString();
        }

        var hmac = rawFields.TryGetValue("hmac", out var h) ? h : string.Empty;
        var txnId = rawFields.TryGetValue("id", out var id) ? id : string.Empty;
        var orderId = rawFields.TryGetValue("order", out var o) ? o : (rawFields.TryGetValue("order_id", out var oid) ? oid : string.Empty);
        var success = rawFields.TryGetValue("success", out var s) && bool.TryParse(s, out var b) && b;
        var amountCents = rawFields.TryGetValue("amount_cents", out var ac) && decimal.TryParse(ac, out var a) ? a : 0m;

        var data = new PaymentCallbackData(txnId, orderId, success, amountCents, hmac, rawFields);
        return Ok(await handler.HandleAsync(data, ct));
    }

    private static void ExtractFields(System.Text.Json.Nodes.JsonNode? node, Dictionary<string, string> fields, string prefix)
    {
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                ExtractFields(kvp.Value, fields, key);
            }
        }
        else if (node is System.Text.Json.Nodes.JsonValue val)
        {
            fields[prefix] = val.ToString();
        }
    }
}
