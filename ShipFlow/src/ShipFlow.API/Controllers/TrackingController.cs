using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipFlow.API.Configuration;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Application.Features.Tracking;

namespace ShipFlow.API.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class TrackingController : ControllerBase
{
    /// <summary>Submits a GPS tracking point. Per-user rate-limited. Driver role only.</summary>
    [HttpPost("api/tracking")]
    [Authorize(Policy = Policies.RequireDriver)]
    [EnableRateLimiting(RateLimitingSetup.TrackingPolicy)]
    [ProducesResponseType(typeof(TrackingPointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TrackingPointDto>> SubmitPoint(
        [FromBody] SubmitTrackingPointCommand command,
        [FromServices] SubmitTrackingPointHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(command, ct));

    /// <summary>Gets the latest reported GPS position for a shipment.</summary>
    [HttpGet("api/shipments/{shipmentId:guid}/tracking/latest")]
    [Authorize(Policy = Policies.RequireShipmentViewer)]
    [ProducesResponseType(typeof(TrackingPointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrackingPointDto>> GetLatest(
        Guid shipmentId,
        [FromServices] GetLatestTrackingHandler handler,
        CancellationToken ct)
    {
        var point = await handler.HandleAsync(shipmentId, ct);
        return point is not null ? Ok(point) : NotFound();
    }

    /// <summary>Gets paginated GPS tracking history for a shipment with optional time filtering.</summary>
    [HttpGet("api/shipments/{shipmentId:guid}/tracking")]
    [Authorize(Policy = Policies.RequireShipmentViewer)]
    [ProducesResponseType(typeof(PagedResult<TrackingPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TrackingPointDto>>> GetHistory(
        Guid shipmentId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromServices] GetTrackingHistoryHandler handler = null!,
        CancellationToken ct = default) =>
        Ok(await handler.HandleAsync(new TrackingHistoryQuery(shipmentId, fromUtc, toUtc, page, pageSize), ct));

    /// <summary>Driver records pickup: Assigned to PickedUp.</summary>
    [HttpPost("api/shipments/{shipmentId:guid}/pickup")]
    [Authorize(Policy = Policies.RequireDriver)]
    [ProducesResponseType(typeof(ShipmentSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShipmentSummaryDto>> PickUp(
        Guid shipmentId,
        [FromServices] PickUpShipmentHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, ct));

    /// <summary>Driver starts transit: PickedUp to InTransit.</summary>
    [HttpPost("api/shipments/{shipmentId:guid}/start-transit")]
    [Authorize(Policy = Policies.RequireDriver)]
    [ProducesResponseType(typeof(ShipmentSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShipmentSummaryDto>> StartTransit(
        Guid shipmentId,
        [FromServices] StartTransitHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, ct));

    /// <summary>Driver delivers: InTransit to Delivered with atomic delivery proof.</summary>
    [HttpPost("api/shipments/{shipmentId:guid}/deliver")]
    [Authorize(Policy = Policies.RequireDriver)]
    [ProducesResponseType(typeof(DeliveryProofDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryProofDto>> Deliver(
        Guid shipmentId,
        [FromBody] DeliverShipmentCommand command,
        [FromServices] DeliverShipmentHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, command, ct));

    /// <summary>Driver reports delivery failure: InTransit to DeliveryFailed.</summary>
    [HttpPost("api/shipments/{shipmentId:guid}/fail-delivery")]
    [Authorize(Policy = Policies.RequireDriver)]
    [ProducesResponseType(typeof(ShipmentSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShipmentSummaryDto>> FailDelivery(
        Guid shipmentId,
        [FromBody] FailDeliveryCommand command,
        [FromServices] FailDeliveryHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, command, ct));

    /// <summary>Driver retries delivery: DeliveryFailed to InTransit.</summary>
    [HttpPost("api/shipments/{shipmentId:guid}/retry-delivery")]
    [Authorize(Policy = Policies.RequireDriver)]
    [ProducesResponseType(typeof(ShipmentSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShipmentSummaryDto>> RetryDelivery(
        Guid shipmentId,
        [FromServices] RetryDeliveryHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, ct));

    /// <summary>Gets delivery proof for a delivered shipment.</summary>
    [HttpGet("api/shipments/{shipmentId:guid}/delivery-proof")]
    [Authorize(Policy = Policies.RequireShipmentViewer)]
    [ProducesResponseType(typeof(DeliveryProofDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryProofDto>> GetDeliveryProof(
        Guid shipmentId,
        [FromServices] GetDeliveryProofHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, ct));

    /// <summary>Gets the driver's current active assignment.</summary>
    [HttpGet("api/driver/assignments/current")]
    [Authorize(Policy = Policies.RequireDriver)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDto>> GetCurrentAssignment(
        [FromServices] GetCurrentDriverAssignmentHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(ct));

    /// <summary>Gets the driver's paginated assignment history.</summary>
    [HttpGet("api/driver/assignments")]
    [Authorize(Policy = Policies.RequireDriver)]
    [ProducesResponseType(typeof(PagedResult<AssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AssignmentDto>>> GetAssignmentHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromServices] GetDriverAssignmentHistoryHandler handler = null!,
        CancellationToken ct = default) =>
        Ok(await handler.HandleAsync(page, pageSize, ct));
}
