using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Quotations;
using ShipFlow.Application.Features.Shipments;

namespace ShipFlow.API.Controllers;

/// <summary>
/// Shipments and their quotes. Authorization is two-layered: role policies here say what kind of user may call an
/// endpoint; the handlers decide whether this user may touch this shipment (ownership, carrier company).
/// There is deliberately no endpoint that sets a status: transitions happen only through the named actions below.
/// </summary>
[ApiController]
[Route("api/shipments")]
[Produces("application/json")]
public sealed class ShipmentsController : ControllerBase
{
    /// <summary>Creates a Pending shipment owned by the calling customer.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireCustomer)]
    [ProducesResponseType(typeof(ShipmentDetailsDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShipmentCommand command, [FromServices] CreateShipmentHandler handler, CancellationToken ct)
    {
        var shipment = await handler.HandleAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = shipment.Id }, shipment);
    }

    /// <summary>Lists shipments the caller may see, with filtering and paging.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.RequireShipmentViewer)]
    [ProducesResponseType(typeof(PagedResult<ShipmentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ShipmentSummaryDto>>> List(
        [FromQuery] GetShipmentsQuery query, [FromServices] GetShipmentsHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.RequireShipmentViewer)]
    [ProducesResponseType(typeof(ShipmentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentDetailsDto>> GetById(
        Guid id, [FromServices] GetShipmentByIdHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, ct));

    /// <summary>Pending → Quoting. Owner only.</summary>
    [HttpPost("{id:guid}/open-for-quotes")]
    [Authorize(Policy = Policies.RequireCustomer)]
    public async Task<ActionResult<ShipmentDetailsDto>> OpenForQuotes(
        Guid id, [FromServices] OpenForQuotingHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, ct));

    /// <summary>Cancels per the lifecycle rules for the caller's capacity (customer, carrier company, platform admin).</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.RequireShipmentViewer)]
    public async Task<ActionResult<ShipmentDetailsDto>> Cancel(
        Guid id, [FromBody] CancelShipmentCommand command, [FromServices] CancelShipmentHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, command, ct));

    /// <summary>A transport company submits a quote. The company is taken from the caller's membership.</summary>
    [HttpPost("{id:guid}/quotes")]
    [Authorize(Policy = Policies.RequireDispatcher)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitQuote(
        Guid id, [FromBody] SubmitQuoteCommand command, [FromServices] SubmitQuoteHandler handler, CancellationToken ct)
    {
        var quote = await handler.HandleAsync(id, command, ct);
        return Created($"/api/shipments/{id}/quotes", quote);
    }

    /// <summary>Owner/admin: all quotes. A company: only its own.</summary>
    [HttpGet("{id:guid}/quotes")]
    [Authorize(Policy = Policies.RequireShipmentViewer)]
    public async Task<ActionResult<IReadOnlyList<QuoteDto>>> GetQuotes(
        Guid id, [FromServices] GetQuotesHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, ct));

    [HttpPost("{id:guid}/quotes/{quoteId:guid}/accept")]
    [Authorize(Policy = Policies.RequireCustomer)]
    [ProducesResponseType(typeof(ShipmentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipmentDetailsDto>> AcceptQuote(
        Guid id, Guid quoteId, [FromServices] AcceptQuoteHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, quoteId, ct));

    [HttpPost("{id:guid}/quotes/{quoteId:guid}/withdraw")]
    [Authorize(Policy = Policies.RequireDispatcher)]
    public async Task<ActionResult<QuoteDto>> WithdrawQuote(
        Guid id, Guid quoteId, [FromServices] WithdrawQuoteHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, quoteId, ct));
}
