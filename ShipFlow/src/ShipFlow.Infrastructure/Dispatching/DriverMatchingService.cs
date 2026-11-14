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

    public async Task<IReadOnlyList<EligibleDriverDto>> GetEligibleDriversAsync(Guid shipmentId, CancellationToken ct)
    {
        var shipment = await _shipments.FindAsync(shipmentId, ct)
            ?? throw new NotFoundException("Shipment not found.");

        var carrierId = shipment.CarrierCompanyId
            ?? throw new ConflictException("Shipment does not have an assigned carrier company.");

        // Fetch drivers and vehicles similar to GetMatchingStatusAsync
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

            var canCarryPayload = hasVehicle && vehicle!.Capacity.MaxWeightKg >= shipment.Cargo.WeightKg && vehicle.Capacity.MaxVolumeM3 >= shipment.Cargo.VolumeM3;
            var canCarryDimensions = hasVehicle && CheckVehicleFitsDimensions(vehicle!, shipment.Cargo);

            string statusText;
            if (!hasVehicle)
                statusText = "No active vehicle assigned";
            else if (!driver.IsAvailable)
                statusText = "Driver is offline";
            else if (isDriverBusy)
                statusText = "Driver currently on active delivery";
            else if (isVehicleBusy)
                statusText = "Assigned vehicle busy on another delivery";
            else if (!canCarryPayload)
                statusText = "Vehicle capacity insufficient for cargo";
            else if (!canCarryDimensions)
                statusText = "Vehicle dimensions not optimal for cargo size";
            else
                statusText = "Eligible & Available";

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
                EligibilityStatus: statusText,
                VehicleModel: vehicle?.ModelName ?? vehicle?.PlateNumber
            ));
        }

        // Return only drivers marked as eligible
        return eligibleList.Where(d => d.EligibilityStatus.StartsWith("Eligible")).ToList();
    }(Guid shipmentId, Viewer viewer, CancellationToken ct)
    {
        var shipment = await _shipments.FindAsync(shipmentId, ct)
            ?? throw new NotFoundException("Shipment not found.");

        if (!viewer.IsPlatformAdmin && !viewer.IsCarrierOf(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        var carrierId = shipment.CarrierCompanyId
            ?? throw new ConflictException("Shipment does not have an assigned carrier company.");

        // Fetch drivers and vehicles similar to GetMatchingStatusAsync
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

            var canCarryPayload = hasVehicle && vehicle!.Capacity.MaxWeightKg >= shipment.Cargo.WeightKg && vehicle.Capacity.MaxVolumeM3 >= shipment.Cargo.VolumeM3;
            var canCarryDimensions = hasVehicle && CheckVehicleFitsDimensions(vehicle!, shipment.Cargo);

            string statusText;
            if (!hasVehicle)
                statusText = "No active vehicle assigned";
            else if (!driver.IsAvailable)
                statusText = "Driver is offline";
            else if (isDriverBusy)
                statusText = "Driver currently on active delivery";
            else if (isVehicleBusy)
                statusText = "Assigned vehicle busy on another delivery";
            else if (!canCarryPayload)
                statusText = "Vehicle capacity insufficient for cargo";
            else if (!canCarryDimensions)
                statusText = "Vehicle dimensions not optimal for cargo size";
            else
                statusText = "Eligible & Available";

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
                EligibilityStatus: statusText,
                VehicleModel: vehicle?.ModelName ?? vehicle?.PlateNumber
            ));
        }

        // Return only eligible drivers (filter status containing "Eligible")
        return eligibleList.Where(d => d.EligibilityStatus.StartsWith("Eligible")).ToList();
    }

    public async Task<bool> TryAssignDriverAsync(Guid shipmentId, Guid driverId, Guid matchingSessionId, Viewer viewer, CancellationToken ct)
    {
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, ct)
            ?? throw new NotFoundException("Shipment not found.");

        // Verify viewer permissions
        if (!viewer.IsPlatformAdmin && !viewer.IsCarrierOf(shipment))
        {
            throw new ForbiddenException("Only carrier admins can assign drivers.");
        }

        var carrierId = shipment.CarrierCompanyId
            ?? throw new ConflictException("Shipment does not have an assigned carrier company.");

        // Load driver
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.Id == driverId && d.CompanyId == carrierId && d.IsActive, ct)
            ?? throw new NotFoundException("Driver not found.");

        // Basic availability checks
        if (!driver.IsAvailable) return false;
        if (driver.AssignedVehicleId == null) return false;

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == driver.AssignedVehicleId && v.IsActive, ct)
            ?? throw new NotFoundException("Vehicle not found.");

        // Ensure driver/vehicle are not already busy
        var driverBusy = await _assignments.HasActiveForDriverAsync(driver.Id, ct);
        var vehicleBusy = await _assignments.HasActiveForVehicleAsync(vehicle.Id, ct);
        if (driverBusy || vehicleBusy) return false;

        // Capacity / dimension check
        if (!CheckVehicleFitsCargo(vehicle, shipment.Cargo)) return false;

        // Verify matching session is still active
        var session = await _db.MatchingSessions
            .FirstOrDefaultAsync(s => s.Id == matchingSessionId && s.ShipmentId == shipmentId, ct);
        if (session == null || session.Status != MatchingSessionStatus.Active)
            return false;

        // Create assignment within transaction
        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var window = TimeWindow.Of(now, now.AddHours(12)); // simplified window
            var existingAssignments = await _assignments.ListActiveOverlappingForAsync(driver.Id, vehicle.Id, window, ct);
            var assignment = Assignment.Create(shipment, driver, vehicle, window, viewer.UserId, existingAssignments, now);
            await _assignments.AddAsync(assignment, ct);

            // Update driver availability
            driver.SetAvailable(false);

            // Mark shipment as assigned
            shipment.AssignDriver(driver.Id, vehicle.Id, now);

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        // Notify parties
        await _notifier.NotifyShipmentTakenAsync(driver.CompanyId, shipment.Id, driver.Id);
        await _notifier.NotifyShipmentAssignedAsync(
            driver.CompanyId,
            shipment.CustomerId,
            shipment.Id,
            driver.FullName,
            vehicle.PlateNumber);

        return true;
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

            var canCarryDimensions = hasVehicle && CheckVehicleFitsDimensions(vehicle!, shipment.Cargo);

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
                EligibilityStatus: statusText,
                VehicleModel: vehicle?.ModelName ?? vehicle?.PlateNumber
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
                        EligibilityStatus: "Assigned",
                        VehicleModel: assignedVeh?.ModelName ?? assignedVeh?.PlateNumber
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

        if (!viewer.IsPlatformAdmin && (!viewer.CanDispatch || !viewer.IsCarrierOf(shipment)))
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

        if (!viewer.IsPlatformAdmin && (!viewer.CanDispatch || !viewer.IsCarrierOf(shipment)))
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

            // Vehicle capacity and dimension check
            if (!CheckVehicleFitsCargo(vehicle, s.Cargo))
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

        if (!CheckVehicleFitsCargo(vehicle, shipment.Cargo))
        {
            throw new ConflictException("Your assigned vehicle does not have sufficient capacity or dimensions for this shipment.");
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
            HasActiveAssignment: hasActive,
            AssignedVehicleModel: vehicle?.ModelName ?? vehicle?.PlateNumber
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

    private static bool CheckVehicleFitsCargo(Vehicle vehicle, Cargo cargo)
    {
        if (vehicle.Capacity.MaxWeightKg < cargo.WeightKg || vehicle.Capacity.MaxVolumeM3 < cargo.VolumeM3)
        {
            return false;
        }

        return CheckVehicleFitsDimensions(vehicle, cargo);
    }

    private static bool CheckVehicleFitsDimensions(Vehicle vehicle, Cargo cargo)
    {
        // 1. If vehicle has actual stored specifications, check against physical constraints
        if (vehicle.CargoLengthM.HasValue && vehicle.CargoWidthM.HasValue && vehicle.CargoHeightM.HasValue)
        {
            // Check for explicit length requirements in cargo description (e.g. "(Max Length 3.1m)")
            var match = System.Text.RegularExpressions.Regex.Match(
                cargo.Description,
                @"(?:Max\s+Length|Length[:\s]+)\s*(\d+(?:\.\d+)?)\s*m",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var requiredLengthM))
            {
                if (vehicle.CargoLengthM.Value < requiredLengthM)
                {
                    return false;
                }
            }

            // Check if cargo requires weather protection or high clearance (e.g. furniture suites, textiles, electronics)
            if (vehicle.Type == VehicleType.Pickup)
            {
                var isWeatherSensitiveOrBulky =
                    cargo.Description.Contains("Furniture", StringComparison.OrdinalIgnoreCase) ||
                    cargo.Description.Contains("Wardrobe", StringComparison.OrdinalIgnoreCase) ||
                    cargo.Description.Contains("Textile", StringComparison.OrdinalIgnoreCase) ||
                    cargo.Description.Contains("Apparel", StringComparison.OrdinalIgnoreCase) ||
                    cargo.VolumeM3 > 3.0m;

                if (isWeatherSensitiveOrBulky)
                {
                    return false;
                }
            }

            // Large furniture suite requiring tall interior height (>= 1.6m) and length (>= 3.0m)
            if (cargo.Description.Contains("Wardrobe", StringComparison.OrdinalIgnoreCase) ||
                cargo.Description.Contains("Living Room", StringComparison.OrdinalIgnoreCase))
            {
                if (vehicle.CargoHeightM.Value < 1.6m || vehicle.CargoLengthM.Value < 3.0m)
                {
                    return false;
                }
            }
        }

        // 2. Category range fallback
        return vehicle.Type switch
        {
            VehicleType.Van => cargo.VolumeM3 <= 14.5m && cargo.WeightKg <= 2000m,
            VehicleType.Pickup => cargo.VolumeM3 <= 18m && cargo.WeightKg <= 2600m,
            VehicleType.Truck => cargo.VolumeM3 <= 76m && cargo.WeightKg <= 12500m,
            VehicleType.Trailer => cargo.VolumeM3 <= 120m && cargo.WeightKg <= 28000m,
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
