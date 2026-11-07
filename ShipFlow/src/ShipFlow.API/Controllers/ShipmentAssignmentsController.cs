using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Application.Features.Shipments;

namespace ShipFlow.API.Controllers;

/// <summary>
/// Dispatching: which resources are free for a shipment, assigning them, releasing them, and persistent Uber-style matching.
/// Only an administrator or dispatcher of the shipment's carrier company may call these.
/// </summary>
[ApiController]
[Route("api/shipments/{shipmentId:guid}")]
[Produces("application/json")]
[Authorize(Policy = Policies.RequireDispatcher)]
public sealed class ShipmentAssignmentsController : ControllerBase
{
    /// <summary>Active vehicles of the carrier that fit the cargo and are free in the window.</summary>
    [HttpGet("available-vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> AvailableVehicles(
        Guid shipmentId, [FromQuery] AvailabilityQuery query, [FromServices] GetAvailableVehiclesHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, query, ct));

    /// <summary>Active drivers of the carrier that are free in the window.</summary>
    [HttpGet("available-drivers")]
    public async Task<ActionResult<IReadOnlyList<DriverDto>>> AvailableDrivers(
        Guid shipmentId, [FromQuery] AvailabilityQuery query, [FromServices] GetAvailableDriversHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, query, ct));

    /// <summary>
    /// Accepted to Assigned. 409 <c>assignment_conflict</c> when the driver or vehicle is booked in an overlapping window
    /// (also when a competing request won the race), 422 <c>capacity_exceeded</c> when the cargo does not fit.
    /// </summary>
    [HttpPost("assignment")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assign(
        Guid shipmentId, [FromBody] AssignShipmentCommand command, [FromServices] AssignShipmentHandler handler, CancellationToken ct)
    {
        var assignment = await handler.HandleAsync(shipmentId, command, ct);
        return Created($"/api/shipments/{shipmentId}/assignment", assignment);
    }

    [HttpGet("assignment")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDto>> GetAssignment(
        Guid shipmentId, [FromServices] GetShipmentAssignmentHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, ct));

    /// <summary>Assigned to Accepted, releasing the driver and vehicle so the shipment can be assigned again.</summary>
    [HttpPost("unassign")]
    public async Task<ActionResult<ShipmentDetailsDto>> Unassign(
        Guid shipmentId, [FromBody] UnassignShipmentCommand command, [FromServices] UnassignShipmentHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(shipmentId, command, ct));

    /// <summary>Gets the current matching status, eligible drivers, and assigned driver for this shipment.</summary>
    [HttpGet("matching-status")]
    public async Task<ActionResult<MatchingStatusDto>> GetMatchingStatus(
        Guid shipmentId,
        [FromServices] IDriverMatchingService service,
        [FromServices] ViewerResolver viewers,
        CancellationToken ct)
    {
        var viewer = await viewers.ResolveAsync(ct);
        return Ok(await service.GetMatchingStatusAsync(shipmentId, viewer, ct));
    }

    /// <summary>Initiates persistent Uber-style matching: broadcast to all eligible available drivers until accepted.</summary>
    [HttpPost("dispatch-matching")]
    public async Task<ActionResult<MatchingStatusDto>> DispatchMatching(
        Guid shipmentId,
        [FromServices] IDriverMatchingService service,
        [FromServices] ViewerResolver viewers,
        CancellationToken ct)
    {
        var viewer = await viewers.ResolveAsync(ct);
        return Ok(await service.DispatchForMatchingAsync(shipmentId, viewer, ct));
    }

    /// <summary>Cancels ongoing driver matching dispatch, returning shipment to Accepted status.</summary>
    [HttpPost("cancel-dispatch-matching")]
    public async Task<ActionResult<MatchingStatusDto>> CancelDispatchMatching(
        Guid shipmentId,
        [FromServices] IDriverMatchingService service,
        [FromServices] ViewerResolver viewers,
        CancellationToken ct)
    {
        var viewer = await viewers.ResolveAsync(ct);
        return Ok(await service.CancelDispatchMatchingAsync(shipmentId, viewer, ct));
    }
}
