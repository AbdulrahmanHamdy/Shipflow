using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Common.Interfaces;

namespace ShipFlow.API.Controllers;

/// <summary>
/// Handles the live vehicle search flow for a shipment.
/// </summary>
[ApiController]
[Route("api/vehicle-search")]
[Produces("application/json")]
public sealed class VehicleSearchController : ControllerBase
{
    private readonly IVehicleSearchService _service;
    private readonly ICurrentUser _currentUser;

    public VehicleSearchController(IVehicleSearchService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Starts a live search session for the given shipment. The caller must be the owning customer.
    /// Returns the matching session id and the list of eligible vehicles.
    /// </summary>
    [HttpPost("{shipmentId:guid}")]
    [Authorize(Policy = Policies.RequireCustomer)]
    public async Task<IActionResult> StartSearch(Guid shipmentId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
        {
            return Unauthorized();
        }

        var (sessionId, eligibleVehicles) = await _service.StartSearchAsync(shipmentId, _currentUser.UserId.Value);
        return Ok(new { SessionId = sessionId, EligibleVehicles = eligibleVehicles });
    }

    /// <summary>
    /// Called by a driver to accept the shipment during an active matching session.
    /// </summary>
    [HttpPost("{sessionId:guid}/accept")]
    [Authorize(Policy = Policies.RequireDriver)]
    public async Task<IActionResult> Accept(Guid sessionId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
        {
            return Unauthorized();
        }

        await _service.AcceptAsync(sessionId, _currentUser.UserId.Value);
        return Ok(new { Message = "Accepted" });
    }

    /// <summary>
    /// Called by the customer to skip the live search (or when the search times out).
    /// </summary>
    [HttpPost("{sessionId:guid}/skip")]
    [Authorize(Policy = Policies.RequireCustomer)]
    public async Task<IActionResult> Skip(Guid sessionId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
        {
            return Unauthorized();
        }

        await _service.SkipAsync(sessionId, _currentUser.UserId.Value);
        return Ok(new { Message = "Search skipped" });
    }
}
