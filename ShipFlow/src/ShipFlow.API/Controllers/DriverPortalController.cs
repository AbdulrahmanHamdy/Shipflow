using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Dispatching;

namespace ShipFlow.API.Controllers;

/// <summary>
/// Endpoints for the driver mobile cockpit: viewing compatible open shipments,
/// accepting matching assignments, and managing availability / assigned vehicle.
/// </summary>
[ApiController]
[Route("api/driver")]
[Produces("application/json")]
[Authorize(Policy = Policies.RequireDriver)]
public sealed class DriverPortalController : ControllerBase
{
    private readonly IDriverMatchingService _matchingService;
    private readonly ICurrentUser _currentUser;

    public DriverPortalController(IDriverMatchingService matchingService, ICurrentUser currentUser)
    {
        _matchingService = matchingService;
        _currentUser = currentUser;
    }

    /// <summary>Retrieves the authenticated driver's profile, assigned vehicle, and online availability.</summary>
    [HttpGet("profile")]
    public async Task<ActionResult<DriverProfileDto>> GetProfile(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Authentication is required.");
        return Ok(await _matchingService.GetDriverProfileAsync(userId, ct));
    }

    /// <summary>Toggles driver availability (Online/Offline).</summary>
    [HttpPut("availability")]
    public async Task<IActionResult> UpdateAvailability([FromBody] UpdateDriverAvailabilityCommand command, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Authentication is required.");
        await _matchingService.UpdateDriverAvailabilityAsync(userId, command.IsAvailable, ct);
        return NoContent();
    }

    /// <summary>Lists open dispatched shipments compatible with the driver's vehicle and availability.</summary>
    [HttpGet("available-shipments")]
    public async Task<ActionResult<IReadOnlyList<AvailableShipmentDto>>> GetAvailableShipments(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Authentication is required.");
        return Ok(await _matchingService.GetAvailableShipmentsForDriverAsync(userId, ct));
    }

    /// <summary>Accepts an open dispatched shipment. Atomic and concurrency-safe.</summary>
    [HttpPost("available-shipments/{shipmentId:guid}/accept")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignmentDto>> AcceptShipment(Guid shipmentId, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Authentication is required.");
        var assignment = await _matchingService.AcceptShipmentAsync(userId, shipmentId, ct);
        return Ok(assignment);
    }
}
