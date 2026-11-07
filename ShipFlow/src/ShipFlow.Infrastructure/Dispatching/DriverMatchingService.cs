using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;
using ShipFlow.Infrastructure.Persistence;

namespace ShipFlow.Infrastructure.Dispatching;

public sealed class DriverMatchingService : IDriverMatchingService
{
    private readonly AppDbContext _db;
    private readonly IShipmentStore _shipments;
    private readonly IAssignmentStore _assignments;
    private readonly IFleetStore _fleet;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatchNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ILogger<DriverMatchingService> _logger;

    public DriverMatchingService(
        AppDbContext db,
        IShipmentStore shipments,
        IAssignmentStore assignments,
        IFleetStore fleet,
        IUnitOfWork unitOfWork,
        IDispatchNotifier notifier,
        TimeProvider time,
        ILogger<DriverMatchingService> logger)
    {
        _db = db;
        _shipments = shipments;
        _assignments = assignments;
        _fleet = fleet;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _time = time;
        _logger = logger;
    }

    public async Task<MatchingStatusDto> GetMatchingStatusAsync(Guid shipmentId, Viewer viewer, CancellationToken ct)
    {
        var shipment = await _shipments.FindAsync(shipmentId, ct)
            ?? throw new NotFoundException("Shipment not found.");

        if (!viewer.IsPlatformAdmin && !viewer.IsCarrierOf(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        var carrierId = shipment.CarrierCompanyId
            ?? throw new ConflictException("Shipment does not have an assigned carrier company.");

        var requiredType = DetermineRequiredVehicleType(shipment.Cargo.WeightKg, shipment.Cargo.VolumeM3);

        // Fetch carrier drivers and their assigned vehicles
        var drivers = await _db.Drivers
            .AsNoTracking()
            .Where(d => d.CompanyId == carrierId && d.IsActive)
            .OrderBy(d => d.FullName)
            .ToListAsync(ct);

        var vehicleIds = drivers.Where(d => d.AssignedVehicleId != null).Select(d => d.AssignedVehicleId!.Value).Distinct().ToList();
        var vehicles = await _db.Vehicles
            .AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        // Fetch active assignments to determine current busy drivers and vehicles
        var activeAssignments = await _db.Assignments
            .AsNoTracking()
            .Where(a => a.CompanyId == carrierId && a.Status == AssignmentStatus.Active)
            .ToListAsync(ct);

        var busyDrivers = activeAssignments.Select(a => a.DriverId).ToHashSet();
        var busyVehicles = activeAssignments.Select(a => a.VehicleId).ToHashSet();

        var eligibleList = new List<EligibleDriverDto>();

        foreach (var driver in drivers)
        {
            Vehicle? vehicle = null;
            if (driver.AssignedVehicleId is { } vId)
            {
                vehicles.TryGetValue(vId, out vehicle);
            }

            var hasVehicle = vehicle != null && vehicle.IsActive;
            var isDriverBusy = busyDrivers.Contains(driver.Id);
            var isVehicleBusy = hasVehicle && busyVehicles.Contains(vehicle!.Id);

            var canCarryPayload = hasVehicle
                && vehicle!.Capacity.MaxWeightKg >= shipment.Cargo.WeightKg
                && vehicle.Capacity.MaxVolumeM3 >= shipment.Cargo.VolumeM3;

            var canCarryDimensions = hasVehicle && CheckDimensionsFit(vehicle!.Type, shipment.Cargo.VolumeM3, shipment.Cargo.WeightKg);

            string statusText;
            if (!hasVehicle)
            {
                statusText = "No active vehicle assigned";
            }
            else if (!driver.IsAvailable)
            {
                statusText = "Driver is offline";
            }
            else if (isDriverBusy)
            {
                statusText = "Driver currently on active delivery";
            }
            else if (isVehicleBusy)
            {
                statusText = "Assigned vehicle busy on another delivery";
            }
            else if (!canCarryPayload)
            {
                statusText = "Vehicle capacity insufficient for cargo";
            }
            else if (!canCarryDimensions)
            {
                statusText = "Vehicle dimensions not optimal for cargo size";
            }
            else
            {
                statusText = "Eligible & Available";
            }

            eligibleList.Add(new EligibleDriverDto(
                DriverId: driver.Id,
                UserId: driver.UserId,
                FullName: driver.FullName,
                LicenseNumber: driver.LicenseNumber,
                IsAvailable: driver.IsAvailable && !isDriverBusy,
                VehicleId: vehicle?.Id,
                VehiclePlate: vehicle?.PlateNumber,
                VehicleType: vehicle?.Type.ToString(),
                VehicleMaxWeightKg: vehicle?.Capacity.MaxWeightKg,
                VehicleMaxVolumeM3: vehicle?.Capacity.MaxVolumeM3,
                CanCarryDimensions: canCarryDimensions,
                EligibilityStatus: statusText
            ));
        }

        EligibleDriverDto? assignedDriver = null;
        if (shipment.CurrentAssignmentId is { } assignId)
        {
            var activeAssign = activeAssignments.FirstOrDefault(a => a.Id == assignId)
                ?? await _db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assignId, ct);

            if (activeAssign != null)
            {
                var assignedDrv = drivers.FirstOrDefault(d => d.Id == activeAssign.DriverId)
                    ?? await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == activeAssign.DriverId, ct);
                var assignedVeh = (activeAssign.VehicleId != Guid.Empty && vehicles.TryGetValue(activeAssign.VehicleId, out var av))
                    ? av
                    : await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == activeAssign.VehicleId, ct);

                if (assignedDrv != null)
                {
                    assignedDriver = new EligibleDriverDto(
                        DriverId: assignedDrv.Id,
                        UserId: assignedDrv.UserId,
                        FullName: assignedDrv.FullName,
                        LicenseNumber: assignedDrv.LicenseNumber,
                        IsAvailable: false,
                        VehicleId: assignedVeh?.Id,
                        VehiclePlate: assignedVeh?.PlateNumber,
                        VehicleType: assignedVeh?.Type.ToString(),
                        VehicleMaxWeightKg: assignedVeh?.Capacity.MaxWeightKg,
                        VehicleMaxVolumeM3: assignedVeh?.Capacity.MaxVolumeM3,
                        CanCarryDimensions: true,
                        EligibilityStatus: "Assigned"
                    );
                }
            }
        }

        var pickup = shipment.PickupStop;
        var delivery = shipment.DeliveryStop;
        var eligibleCount = eligibleList.Count(e => e.EligibilityStatus == "Eligible & Ready" || e.EligibilityStatus == "Eligible & Available");

        return new MatchingStatusDto(
            ShipmentId: shipment.Id,
            Reference: shipment.Reference,
            Status: shipment.Status.ToString(),
            PickupAddress: pickup.Address,
            PickupWindowStartUtc: pickup.Window?.StartUtc,
            PickupWindowEndUtc: pickup.Window?.EndUtc,
            DeliveryAddress: delivery.Address,
            DeliveryWindowStartUtc: delivery.Window?.StartUtc,
            DeliveryWindowEndUtc: delivery.Window?.EndUtc,
            CargoDescription: shipment.Cargo.Description,
            CargoWeightKg: shipment.Cargo.WeightKg,
            CargoVolumeM3: shipment.Cargo.VolumeM3,
            RequiredVehicleType: requiredType.ToString(),
            EligibleDriversCount: eligibleCount,
            NotifiedDriversCount: eligibleCount,
            EligibleDrivers: eligibleList,
            AssignedDriver: assignedDriver,
            DispatchedAtUtc: shipment.Status == ShipmentStatus.SearchingForDriver ? shipment.UpdatedAtUtc : null
        );
    }

    public async Task<MatchingStatusDto> DispatchForMatchingAsync(Guid shipmentId, Viewer viewer, CancellationToken ct)
    {
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, ct)
            ?? throw new NotFoundException("Shipment not found.");

        if (!viewer.CanDispatch || !viewer.IsCarrierOf(shipment))
        {
            throw new ForbiddenException("Only an administrator or dispatcher of the assigned carrier can dispatch this shipment.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        shipment.MarkSearchingForDriver(viewer.UserId, now);
        await _unitOfWork.SaveChangesAsync(ct);

        var refCode = shipment.Reference ?? $"SF-{shipment.Id.ToString()[..8].ToUpperInvariant()}";
        await _notifier.NotifyNewShipmentAvailableAsync(
            shipment.CarrierCompanyId!.Value,
            shipment.Id,
            refCode,
            shipment.Cargo.Description,
            shipment.Cargo.WeightKg,
            shipment.Cargo.VolumeM3);

        _logger.LogInformation("Shipment {ShipmentId} ({Reference}) dispatched for persistent driver matching by {UserId}",
            shipment.Id, refCode, viewer.UserId);

        return await GetMatchingStatusAsync(shipmentId, viewer, ct);
    }

    public async Task<MatchingStatusDto> CancelDispatchMatchingAsync(Guid shipmentId, Viewer viewer, CancellationToken ct)
    {
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, ct)
            ?? throw new NotFoundException("Shipment not found.");

        if (!viewer.CanDispatch || !viewer.IsCarrierOf(shipment))
        {
            throw new ForbiddenException("Only an administrator or dispatcher of the assigned carrier can cancel dispatch.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        shipment.CancelSearchingForDriver(viewer.UserId, now);
        await _unitOfWork.SaveChangesAsync(ct);

        await _notifier.NotifyShipmentCancelledAsync(shipment.CarrierCompanyId!.Value, shipment.Id);

        _logger.LogInformation("Shipment {ShipmentId} driver matching cancelled by {UserId}", shipment.Id, viewer.UserId);

        return await GetMatchingStatusAsync(shipmentId, viewer, ct);
    }

    public async Task<IReadOnlyList<AvailableShipmentDto>> GetAvailableShipmentsForDriverAsync(Guid driverUserId, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == driverUserId && d.IsActive, ct);

        if (driver == null || !driver.IsAvailable || driver.AssignedVehicleId == null)
        {
            return Array.Empty<AvailableShipmentDto>();
        }

        // Check if driver has an active assignment
        var isDriverBusy = await _assignments.HasActiveForDriverAsync(driver.Id, ct);
        if (isDriverBusy)
        {
            return Array.Empty<AvailableShipmentDto>();
        }

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == driver.AssignedVehicleId && v.IsActive, ct);

        if (vehicle == null)
        {
            return Array.Empty<AvailableShipmentDto>();
        }

        var isVehicleBusy = await _assignments.HasActiveForVehicleAsync(vehicle.Id, ct);
        if (isVehicleBusy)
        {
            return Array.Empty<AvailableShipmentDto>();
        }

        // Fetch carrier company name
        var company = await _db.TransportCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == driver.CompanyId, ct);
        var companyName = company?.Name ?? "Carrier Company";

        // Query open shipments for this company with status SearchingForDriver
        var openShipments = await _db.Shipments
            .AsNoTracking()
            .Include(s => s.Stops)
            .Where(s => s.CarrierCompanyId == driver.CompanyId && s.Status == ShipmentStatus.SearchingForDriver && s.CurrentAssignmentId == null)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .ToListAsync(ct);

        var result = new List<AvailableShipmentDto>();

        foreach (var s in openShipments)
        {
            if (s.Stops.Count < 2) continue;

            // Vehicle capacity check
            if (vehicle.Capacity.MaxWeightKg < s.Cargo.WeightKg || vehicle.Capacity.MaxVolumeM3 < s.Cargo.VolumeM3)
            {
                continue;
            }

            // Dimension check
            if (!CheckDimensionsFit(vehicle.Type, s.Cargo.VolumeM3, s.Cargo.WeightKg))
            {
                continue;
            }

            var pickup = s.PickupStop;
            var delivery = s.DeliveryStop;

            var distanceKm = CalculateHaversineDistanceKm(
                pickup.Location.Latitude, pickup.Location.Longitude,
                delivery.Location.Latitude, delivery.Location.Longitude);

            var reqType = DetermineRequiredVehicleType(s.Cargo.WeightKg, s.Cargo.VolumeM3);

            result.Add(new AvailableShipmentDto(
                ShipmentId: s.Id,
                Reference: s.Reference,
                Status: s.Status.ToString(),
                PickupAddress: pickup.Address,
                PickupWindowStartUtc: pickup.Window?.StartUtc,
                PickupWindowEndUtc: pickup.Window?.EndUtc,
                DeliveryAddress: delivery.Address,
                DeliveryWindowStartUtc: delivery.Window?.StartUtc,
                DeliveryWindowEndUtc: delivery.Window?.EndUtc,
                CargoDescription: s.Cargo.Description,
                CargoWeightKg: s.Cargo.WeightKg,
                CargoVolumeM3: s.Cargo.VolumeM3,
                RequiredVehicleType: reqType.ToString(),
                EstimatedDistanceKm: Math.Round(distanceKm, 1),
                PriceAmount: s.AgreedPrice?.Amount,
                PriceCurrency: s.AgreedPrice?.Currency,
                CreatedAtUtc: s.CreatedAtUtc,
                CarrierCompanyName: companyName
            ));
        }

        return result;
    }

    public async Task<AssignmentDto> AcceptShipmentAsync(Guid driverUserId, Guid shipmentId, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.UserId == driverUserId && d.IsActive, ct)
            ?? throw new ForbiddenException("Active driver profile not found.");

        if (!driver.IsAvailable)
        {
            throw new ConflictException("You are currently marked as offline. Go online to accept shipments.");
        }

        if (driver.AssignedVehicleId == null)
        {
            throw new ConflictException("No active vehicle is assigned to your profile.");
        }

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == driver.AssignedVehicleId && v.IsActive, ct)
            ?? throw new ConflictException("Assigned vehicle is not active.");

        // Check driver and vehicle busy state
        var isDriverBusy = await _assignments.HasActiveForDriverAsync(driver.Id, ct);
        if (isDriverBusy)
        {
            throw new ConflictException("You already have an active shipment in progress.");
        }

        var isVehicleBusy = await _assignments.HasActiveForVehicleAsync(vehicle.Id, ct);
        if (isVehicleBusy)
        {
            throw new ConflictException("Your assigned vehicle is currently in use on another shipment.");
        }

        // Concurrency-safe lookup
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, ct)
            ?? throw new NotFoundException("Shipment not found.");

        if (shipment.CarrierCompanyId != driver.CompanyId)
        {
            throw new NotFoundException("Shipment not found.");
        }

        if (shipment.Status != ShipmentStatus.SearchingForDriver || shipment.CurrentAssignmentId != null)
        {
            throw new ConflictException("This shipment has already been accepted by another driver.");
        }

        if (!vehicle.Capacity.CanCarry(shipment.Cargo))
        {
            throw new ConflictException("Your assigned vehicle does not have sufficient capacity for this shipment.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var pickupWindow = shipment.PickupStop.Window;
        TimeWindow window;
        if (pickupWindow != null && pickupWindow.EndUtc > now)
        {
            var start = pickupWindow.StartUtc < now ? now : pickupWindow.StartUtc;
            window = TimeWindow.Of(start, pickupWindow.EndUtc);
        }
        else
        {
            window = TimeWindow.Of(now, now.AddHours(12));
        }

        var existingAssignments = await _assignments.ListActiveOverlappingForAsync(driver.Id, vehicle.Id, window, ct);

        Assignment assignment;
        try
        {
            assignment = Assignment.Create(shipment, driver, vehicle, window, driverUserId, existingAssignments, now);
            await _assignments.AddAsync(assignment, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This shipment has already been accepted by another driver.");
        }

        _logger.LogInformation("Driver {DriverName} ({DriverId}) accepted shipment {ShipmentId}. Assigned to vehicle {VehiclePlate}.",
            driver.FullName, driver.Id, shipment.Id, vehicle.PlateNumber);

        // SignalR notifications:
        // 1. Notify other drivers the shipment is taken
        await _notifier.NotifyShipmentTakenAsync(driver.CompanyId, shipment.Id, driver.Id);

        // 2. Notify dispatchers and customer
        await _notifier.NotifyShipmentAssignedAsync(
            driver.CompanyId,
            shipment.CustomerId,
            shipment.Id,
            driver.FullName,
            vehicle.PlateNumber);

        return assignment.ToDto(shipment.Status);
    }

    public async Task<DriverProfileDto> GetDriverProfileAsync(Guid driverUserId, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == driverUserId && d.IsActive, ct)
            ?? throw new ForbiddenException("Active driver profile not found.");

        var company = await _db.TransportCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == driver.CompanyId, ct);

        Vehicle? vehicle = null;
        if (driver.AssignedVehicleId is { } vId)
        {
            vehicle = await _db.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vId, ct);
        }

        var hasActive = await _assignments.HasActiveForDriverAsync(driver.Id, ct);

        return new DriverProfileDto(
            DriverId: driver.Id,
            UserId: driver.UserId,
            FullName: driver.FullName,
            LicenseNumber: driver.LicenseNumber,
            CompanyId: driver.CompanyId,
            CompanyName: company?.Name ?? "Carrier Company",
            AssignedVehicleId: vehicle?.Id,
            AssignedVehiclePlate: vehicle?.PlateNumber,
            AssignedVehicleType: vehicle?.Type.ToString(),
            IsAvailable: driver.IsAvailable,
            HasActiveAssignment: hasActive
        );
    }

    public async Task UpdateDriverAvailabilityAsync(Guid driverUserId, bool isAvailable, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.UserId == driverUserId && d.IsActive, ct)
            ?? throw new ForbiddenException("Active driver profile not found.");

        driver.SetAvailable(isAvailable);
        await _unitOfWork.SaveChangesAsync(ct);

        await _notifier.NotifyDriverAvailabilityChangedAsync(driver.CompanyId, driver.Id, isAvailable);

        _logger.LogInformation("Driver {DriverName} availability set to {IsAvailable}", driver.FullName, isAvailable);
    }

    public async Task AssignDriverVehicleAsync(Guid companyId, Guid driverId, Guid? vehicleId, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.CompanyId == companyId && d.Id == driverId && d.IsActive, ct)
            ?? throw new NotFoundException("Driver not found.");

        if (vehicleId.HasValue)
        {
            var vehicle = await _db.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.CompanyId == companyId && v.Id == vehicleId.Value && v.IsActive, ct)
                ?? throw new NotFoundException("Vehicle not found.");

            driver.AssignVehicle(vehicle.Id);
        }
        else
        {
            driver.UnassignVehicle();
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Driver {DriverId} assigned vehicle {VehicleId}", driverId, vehicleId);
    }

    private static VehicleType DetermineRequiredVehicleType(decimal weightKg, decimal volumeM3)
    {
        if (weightKg > 12000m || volumeM3 > 75m) return VehicleType.Trailer;
        if (weightKg > 2500m || volumeM3 > 18m) return VehicleType.Truck;
        if (weightKg > 1800m || volumeM3 > 14m) return VehicleType.Pickup;
        return VehicleType.Van;
    }

    private static bool CheckDimensionsFit(VehicleType type, decimal volumeM3, decimal weightKg)
    {
        return type switch
        {
            VehicleType.Van => volumeM3 <= 14.5m && weightKg <= 2000m,
            VehicleType.Pickup => volumeM3 <= 18m && weightKg <= 2600m,
            VehicleType.Truck => volumeM3 <= 76m && weightKg <= 12500m,
            VehicleType.Trailer => volumeM3 <= 120m && weightKg <= 28000m,
            _ => true
        };
    }

    private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0; // Earth's radius in kilometers
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
