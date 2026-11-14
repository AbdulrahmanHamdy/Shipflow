using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Infrastructure.Hubs;
using Hangfire;

namespace ShipFlow.Infrastructure.Services;

/// <summary>
/// Handles the customer‑initiated live vehicle search.
/// It creates a MatchingSession, finds eligible drivers/vehicles,
/// registers a Hangfire job for the 2‑minute timeout and
/// exposes Accept/Skip operations.
/// </summary>
public sealed class VehicleSearchService : IVehicleSearchService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DispatchHub> _hub;
    private readonly IDriverMatchingService _driverMatching;
    private readonly ILogger<VehicleSearchService> _logger;

    public VehicleSearchService(
        AppDbContext db,
        IHubContext<DispatchHub> hub,
        IDriverMatchingService driverMatching,
        ILogger<VehicleSearchService> logger)
    {
        _db = db;
        _hub = hub;
        _driverMatching = driverMatching;
        _logger = logger;
    }

    /// <summary>
    /// Starts a live search for the given shipment.
    /// Returns the created MatchingSession Id and the list of eligible vehicles.
    /// </summary>
    public async Task<(Guid SessionId, List<VehicleDto> EligibleVehicles)> StartSearchAsync(Guid shipmentId, Guid customerId)
    {
        // Verify shipment belongs to the customer and is in a searchable state.
        var shipment = await _db.Shipments
            .Include(s => s.CargoItems)
            .FirstOrDefaultAsync(s => s.Id == shipmentId && s.CustomerId == customerId);
        if (shipment == null)
            throw new InvalidOperationException("Shipment not found or not owned by customer.");

        // Create session
        var now = DateTime.UtcNow;
        var session = new MatchingSession
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            StartedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(2),
            Status = MatchingSessionStatus.Active
        };
        _db.MatchingSessions.Add(session);
        await _db.SaveChangesAsync();

        // Determine eligible drivers/vehicles using existing matching logic.
        var eligible = await _driverMatching.GetEligibleDriversAsync(shipment);
        var vehicleDtos = eligible.Select(d => new VehicleDto
        {
            DriverId = d.DriverId,
            VehicleId = d.AssignedVehicleId ?? Guid.Empty,
            VehicleType = d.VehicleType,
            Latitude = d.Latitude,
            Longitude = d.Longitude,
            PayloadKg = d.PayloadKg,
            CargoLengthM = d.CargoLengthM,
            CargoWidthM = d.CargoWidthM,
            CargoHeightM = d.CargoHeightM,
            DoorWidthM = d.DoorWidthM,
            DoorHeightM = d.DoorHeightM
        }).ToList();

        // Notify eligible drivers via SignalR.
        foreach (var dto in vehicleDtos)
        {
            await _hub.Clients.Group($"driver-{dto.DriverId}")
                .SendAsync("ShipmentOfferReceived", new
                {
                    SessionId = session.Id,
                    ShipmentId = shipment.Id,
                    VehicleId = dto.VehicleId,
                    DriverId = dto.DriverId,
                    PayloadKg = dto.PayloadKg
                });
        }

        // Notify customer that the session started.
        await _hub.Clients.Group($"customer-{customerId}")
            .SendAsync("MatchingSessionStarted", new
            {
                SessionId = session.Id,
                ExpiresAtUtc = session.ExpiresAtUtc,
                EligibleVehicles = vehicleDtos
            });

        // Schedule Hangfire timeout job.
        BackgroundJob.Schedule<IMatchingSessionExpirationJob>(
            job => job.HandleExpirationAsync(session.Id),
            TimeSpan.FromMinutes(2));

        return (session.Id, vehicleDtos);
    }

    public async Task AcceptAsync(Guid sessionId, Guid driverId)
    {
        var session = await _db.MatchingSessions.FirstAsync(s => s.Id == sessionId);
        if (session.Status != MatchingSessionStatus.Active)
            throw new InvalidOperationException("Session is not active.");

        // Attempt atomic assignment.
        var success = await _driverMatching.TryAssignDriverAsync(session.ShipmentId, driverId, sessionId);
        if (!success)
            throw new InvalidOperationException("Driver could not be assigned – possibly already taken.");

        // Update session status.
        session.Status = MatchingSessionStatus.Completed;
        await _db.SaveChangesAsync();

        // Notify all parties.
        await _hub.Clients.Group($"session-{sessionId}")
            .SendAsync("DriverAcceptedShipment", new { SessionId = sessionId, DriverId = driverId });
    }

    public async Task SkipAsync(Guid sessionId, Guid customerId)
    {
        var session = await _db.MatchingSessions.FirstAsync(s => s.Id == sessionId);
        if (session.Status != MatchingSessionStatus.Active)
            return; // already finished

        session.Status = MatchingSessionStatus.Skipped;
        await _db.SaveChangesAsync();

        // Notify dispatcher and customer.
        await _hub.Clients.Group("dispatchers")
            .SendAsync("CustomerSearchSkipped", new { SessionId = sessionId, ShipmentId = session.ShipmentId });
        await _hub.Clients.Group($"customer-{customerId}")
            .SendAsync("CustomerSearchSkipped", new { SessionId = sessionId });
    }
}

// DTO used for sending vehicle info to the client.
public sealed class VehicleDto
{
    public Guid DriverId { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal PayloadKg { get; set; }
    public decimal CargoLengthM { get; set; }
    public decimal CargoWidthM { get; set; }
    public decimal CargoHeightM { get; set; }
    public decimal DoorWidthM { get; set; }
    public decimal DoorHeightM { get; set; }
}
