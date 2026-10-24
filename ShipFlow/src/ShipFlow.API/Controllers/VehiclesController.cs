using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Fleet;

namespace ShipFlow.API.Controllers;

/// <summary>The caller's company vehicles. Same access rules as <see cref="DriversController"/>.</summary>
[ApiController]
[Route("api/fleet/vehicles")]
[Produces("application/json")]
public sealed class VehiclesController : ControllerBase
{
    /// <summary>409 when the plate already exists in the company.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterVehicleCommand command, [FromServices] RegisterVehicleHandler handler, CancellationToken ct)
    {
        var vehicle = await handler.HandleAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, vehicle);
    }

    [HttpGet]
    [Authorize(Policy = Policies.RequireDispatcher)]
    public async Task<ActionResult<PagedResult<VehicleDto>>> List(
        [FromQuery] ListFleetQuery query, [FromServices] ListVehiclesHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.RequireDispatcher)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDto>> GetById(Guid id, [FromServices] GetVehicleHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, ct));

    /// <summary>Capacity can grow at any time; it cannot shrink while the vehicle has an active assignment (409).</summary>
    [HttpPut("{id:guid}/capacity")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    public async Task<ActionResult<VehicleDto>> UpdateCapacity(
        Guid id, [FromBody] UpdateVehicleCapacityCommand command, [FromServices] VehicleManagementHandler handler, CancellationToken ct) =>
        Ok(await handler.UpdateCapacityAsync(id, command, ct));

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    public async Task<ActionResult<VehicleDto>> Activate(
        Guid id, [FromServices] VehicleManagementHandler handler, CancellationToken ct) =>
        Ok(await handler.ActivateAsync(id, ct));

    /// <summary>Refused with 409 while the vehicle has an active assignment.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    public async Task<ActionResult<VehicleDto>> Deactivate(
        Guid id, [FromServices] VehicleManagementHandler handler, CancellationToken ct) =>
        Ok(await handler.DeactivateAsync(id, ct));
}
