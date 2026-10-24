using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Application.Features.Shipments;

namespace ShipFlow.API.Controllers;

/// <summary>
/// Dispatching: which resources are free for a shipment, assigning them, and releasing them. Only an administrator or
/// dispatcher of the shipment's carrier company may call these; for anyone else the shipment, driver and vehicle simply do
/// not exist (404). The shipment status changes only as a consequence of assign/unassign, never directly.
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
}
