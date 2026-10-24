using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Fleet;

namespace ShipFlow.API.Controllers;

/// <summary>
/// The caller's company drivers. Writes need a CompanyAdmin; reads also allow a Dispatcher. The company is always the caller's,
/// so another company's driver id answers 404.
/// </summary>
[ApiController]
[Route("api/fleet/drivers")]
[Produces("application/json")]
public sealed class DriversController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDriverCommand command, [FromServices] RegisterDriverHandler handler, CancellationToken ct)
    {
        var driver = await handler.HandleAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = driver.Id }, driver);
    }

    [HttpGet]
    [Authorize(Policy = Policies.RequireDispatcher)]
    public async Task<ActionResult<PagedResult<DriverDto>>> List(
        [FromQuery] ListFleetQuery query, [FromServices] ListDriversHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.RequireDispatcher)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverDto>> GetById(Guid id, [FromServices] GetDriverHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    public async Task<ActionResult<DriverDto>> Update(
        Guid id, [FromBody] UpdateDriverCommand command, [FromServices] UpdateDriverHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, command, ct));

    /// <summary>Only while the driver's user is still an active Driver member of the company.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    public async Task<ActionResult<DriverDto>> Activate(
        Guid id, [FromServices] DriverActivationHandler handler, CancellationToken ct) =>
        Ok(await handler.ActivateAsync(id, ct));

    /// <summary>Refused with 409 while the driver has an active assignment.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    public async Task<ActionResult<DriverDto>> Deactivate(
        Guid id, [FromServices] DriverActivationHandler handler, CancellationToken ct) =>
        Ok(await handler.DeactivateAsync(id, ct));
}
