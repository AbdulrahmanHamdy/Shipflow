using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;
using ShipFlow.Infrastructure.Dispatching;
using ShipFlow.Infrastructure.Persistence;
using ShipFlow.Infrastructure.Persistence.Stores;
using Xunit;

namespace ShipFlow.Application.Tests.Dispatching;

public sealed class DriverMatchingTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ShipmentStore _shipments;
    private readonly AssignmentStore _assignments;
    private readonly FleetStore _fleet;
    private readonly UnitOfWork _unitOfWork;
    private readonly FakeDispatchNotifier _notifier;
    private readonly DriverMatchingService _service;
    private readonly DateTime _now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    public DriverMatchingTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _shipments = new ShipmentStore(_db);
        _assignments = new AssignmentStore(_db);
        _fleet = new FleetStore(_db);
        _unitOfWork = new UnitOfWork(_db);
        _notifier = new FakeDispatchNotifier();

        _service = new DriverMatchingService(
            _db,
            _shipments,
            _assignments,
            _fleet,
            _unitOfWork,
            _notifier,
            TimeProvider.System,
            NullLogger<DriverMatchingService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task Dispatch_MovesShipmentToSearchingForDriver_AndBroadcastsNotification()
    {
        var (company, dispatcherUser, _, _, shipment) = await SetupCompanyWithFleetAndShipmentAsync();

        var viewer = new Viewer(dispatcherUser, false, company.Id, CompanyRole.Dispatcher);
        var result = await _service.DispatchForMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        Assert.Equal(ShipmentStatus.SearchingForDriver.ToString(), result.Status);
        Assert.Single(_notifier.NewShipmentNotifications);
        Assert.Equal(shipment.Id, _notifier.NewShipmentNotifications[0].ShipmentId);
    }

    [Fact]
    public async Task EligibleDriver_CanSeeDispatchedShipment_AndIneligibleDriverCannot()
    {
        var (company, dispatcherUser, driverUser1, driverUser2, shipment) = await SetupCompanyWithFleetAndShipmentAsync();

        var viewer = new Viewer(dispatcherUser, false, company.Id, CompanyRole.Dispatcher);
        await _service.DispatchForMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        // Driver 1 has a Truck (capacity 10000kg, 60m3) -> should see it
        var availableDriver1 = await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None);
        Assert.Single(availableDriver1);
        Assert.Equal(shipment.Id, availableDriver1[0].ShipmentId);

        // Driver 2 has a small Van (capacity 1000kg, 5m3) while shipment is 2000kg -> should NOT see it
        var availableDriver2 = await _service.GetAvailableShipmentsForDriverAsync(driverUser2, CancellationToken.None);
        Assert.Empty(availableDriver2);
    }

    [Fact]
    public async Task DispatchedShipment_RemainsAvailable_WithoutExpiration()
    {
        var (company, dispatcherUser, driverUser1, _, shipment) = await SetupCompanyWithFleetAndShipmentAsync();

        var viewer = new Viewer(dispatcherUser, false, company.Id, CompanyRole.Dispatcher);
        await _service.DispatchForMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        // Check immediately
        var firstCheck = await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None);
        Assert.Single(firstCheck);

        // Simulate subsequent check after extended time (persistent matching)
        var secondCheck = await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None);
        Assert.Single(secondCheck);
        Assert.Equal(shipment.Id, secondCheck[0].ShipmentId);
    }

    [Fact]
    public async Task NewlyAvailableDriver_ReceivesExistingDispatchedShipments()
    {
        var (company, dispatcherUser, driverUser1, _, shipment) = await SetupCompanyWithFleetAndShipmentAsync();

        // Driver 1 goes offline first
        await _service.UpdateDriverAvailabilityAsync(driverUser1, false, CancellationToken.None);

        var viewer = new Viewer(dispatcherUser, false, company.Id, CompanyRole.Dispatcher);
        await _service.DispatchForMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        // Driver 1 is offline, should see nothing
        var offlineCheck = await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None);
        Assert.Empty(offlineCheck);

        // Driver 1 goes online
        await _service.UpdateDriverAvailabilityAsync(driverUser1, true, CancellationToken.None);

        // Now driver 1 receives the open shipment
        var onlineCheck = await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None);
        Assert.Single(onlineCheck);
        Assert.Equal(shipment.Id, onlineCheck[0].ShipmentId);
    }

    [Fact]
    public async Task FirstDriverAccepts_AssignsShipment_AndBecomesBusy()
    {
        var (company, dispatcherUser, driverUser1, _, shipment) = await SetupCompanyWithFleetAndShipmentAsync();

        var viewer = new Viewer(dispatcherUser, false, company.Id, CompanyRole.Dispatcher);
        await _service.DispatchForMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        var assignment = await _service.AcceptShipmentAsync(driverUser1, shipment.Id, CancellationToken.None);

        Assert.Equal(shipment.Id, assignment.ShipmentId);
        Assert.Equal(AssignmentStatus.Active.ToString(), assignment.Status);

        // Shipment status should be Assigned
        var updatedShipment = await _shipments.FindAsync(shipment.Id, CancellationToken.None);
        Assert.NotNull(updatedShipment);
        Assert.Equal(ShipmentStatus.Assigned, updatedShipment.Status);

        // Driver is now busy, should no longer see available shipments
        var availableAfterAccept = await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None);
        Assert.Empty(availableAfterAccept);

        // Notification was emitted
        Assert.Single(_notifier.ShipmentTakenNotifications);
        Assert.Single(_notifier.ShipmentAssignedNotifications);
    }

    [Fact]
    public async Task SimultaneousAcceptance_OnlyFirstDriverWins_SecondGetsConflict()
    {
        var (company, dispatcherUser, driverUser1, driverUser2, shipment) = await SetupCompanyWithFleetAndShipmentAsync();

        // Upgrade driver 2's vehicle to also be capable
        var driver2 = await _db.Drivers.FirstAsync(d => d.UserId == driverUser2);
        var truck = company.RegisterVehicle("TRK-DUAL", VehicleType.Truck, Capacity.Of(12000m, 60m));
        _db.Vehicles.Add(truck);
        await _db.SaveChangesAsync();
        driver2.AssignVehicle(truck.Id);
        await _db.SaveChangesAsync();

        var viewer = new Viewer(dispatcherUser, false, company.Id, CompanyRole.Dispatcher);
        await _service.DispatchForMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        // Both drivers see the shipment
        Assert.Single(await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None));
        Assert.Single(await _service.GetAvailableShipmentsForDriverAsync(driverUser2, CancellationToken.None));

        // Driver 1 accepts first
        var assign1 = await _service.AcceptShipmentAsync(driverUser1, shipment.Id, CancellationToken.None);
        Assert.NotNull(assign1);

        // Driver 2 attempts to accept after driver 1 won the race
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.AcceptShipmentAsync(driverUser2, shipment.Id, CancellationToken.None));

        Assert.Contains("already been accepted", ex.Message);

        // Driver 2 no longer sees it in available shipments
        var availableDriver2 = await _service.GetAvailableShipmentsForDriverAsync(driverUser2, CancellationToken.None);
        Assert.Empty(availableDriver2);
    }

    [Fact]
    public async Task CancelDispatch_RemovesShipmentFromDriverAvailableList()
    {
        var (company, dispatcherUser, driverUser1, _, shipment) = await SetupCompanyWithFleetAndShipmentAsync();

        var viewer = new Viewer(dispatcherUser, false, company.Id, CompanyRole.Dispatcher);
        await _service.DispatchForMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        // Driver sees it
        Assert.Single(await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None));

        // Dispatcher cancels dispatch
        await _service.CancelDispatchMatchingAsync(shipment.Id, viewer, CancellationToken.None);

        // Shipment status is back to Accepted
        var updatedShipment = await _shipments.FindAsync(shipment.Id, CancellationToken.None);
        Assert.NotNull(updatedShipment);
        Assert.Equal(ShipmentStatus.Accepted, updatedShipment.Status);

        // Driver no longer sees it
        Assert.Empty(await _service.GetAvailableShipmentsForDriverAsync(driverUser1, CancellationToken.None));
        Assert.Single(_notifier.ShipmentCancelledNotifications);
    }

    private async Task<(TransportCompany Company, Guid DispatcherUserId, Guid DriverUser1, Guid DriverUser2, Shipment Shipment)> SetupCompanyWithFleetAndShipmentAsync()
    {
        var adminId = Guid.NewGuid();
        var dispatcherId = Guid.NewGuid();
        var driver1Id = Guid.NewGuid();
        var driver2Id = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var company = TransportCompany.Create("FastFreight Logistics", adminId, _now.AddDays(-10));
        company.AddMember(dispatcherId, CompanyRole.Dispatcher, _now.AddDays(-10));
        company.AddMember(driver1Id, CompanyRole.Driver, _now.AddDays(-10));
        company.AddMember(driver2Id, CompanyRole.Driver, _now.AddDays(-10));
        _db.TransportCompanies.Add(company);
        await _db.SaveChangesAsync();

        var drv1 = company.RegisterDriver(driver1Id, "Ahmed Driver", "LIC-001");
        var drv2 = company.RegisterDriver(driver2Id, "Sami Driver", "LIC-002");
        _db.Drivers.AddRange(drv1, drv2);

        var vehTruck = company.RegisterVehicle("TRK-100", VehicleType.Truck, Capacity.Of(10000m, 60m));
        var vehVan = company.RegisterVehicle("VAN-200", VehicleType.Van, Capacity.Of(1000m, 5m));
        _db.Vehicles.AddRange(vehTruck, vehVan);
        await _db.SaveChangesAsync();

        drv1.AssignVehicle(vehTruck.Id);
        drv1.SetAvailable(true);
        drv2.AssignVehicle(vehVan.Id);
        drv2.SetAvailable(true);
        await _db.SaveChangesAsync();

        var stops = new List<StopInfo>
        {
            new("Cairo Hub, Nasr City", GeoPoint.Of(30.0444, 31.2357), TimeWindow.Of(_now.AddHours(2), _now.AddHours(10))),
            new("Alexandria Port, Gate 4", GeoPoint.Of(31.2001, 29.9187))
        };

        var shipment = Shipment.Create(customerId, Cargo.Of("Industrial Machinery Spares", 2000m, 10m), stops, _now.AddHours(-2), "SF-TEST-001");
        shipment.OpenForQuoting(customerId, _now.AddHours(-1));
        var quote = shipment.SubmitQuote(company.Id, Money.Of(800m, "USD"), _now.AddDays(2), "Standard Freight", _now.AddMinutes(-30));
        shipment.AcceptQuote(customerId, quote.Id, _now.AddMinutes(-10));
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();

        return (company, dispatcherId, driver1Id, driver2Id, shipment);
    }

    private sealed class FakeDispatchNotifier : IDispatchNotifier
    {
        public List<(Guid CompanyId, Guid ShipmentId, string Reference)> NewShipmentNotifications { get; } = [];
        public List<(Guid CompanyId, Guid ShipmentId, Guid WinningDriverId)> ShipmentTakenNotifications { get; } = [];
        public List<(Guid CompanyId, Guid ShipmentId)> ShipmentCancelledNotifications { get; } = [];
        public List<(Guid CompanyId, Guid CustomerId, Guid ShipmentId, string DriverName, string VehiclePlate)> ShipmentAssignedNotifications { get; } = [];
        public List<(Guid CompanyId, Guid DriverId, bool IsAvailable)> DriverAvailabilityNotifications { get; } = [];

        public Task NotifyNewShipmentAvailableAsync(Guid companyId, Guid shipmentId, string reference, string cargoDescription, decimal weightKg, decimal volumeM3)
        {
            NewShipmentNotifications.Add((companyId, shipmentId, reference));
            return Task.CompletedTask;
        }

        public Task NotifyShipmentTakenAsync(Guid companyId, Guid shipmentId, Guid winningDriverId)
        {
            ShipmentTakenNotifications.Add((companyId, shipmentId, winningDriverId));
            return Task.CompletedTask;
        }

        public Task NotifyShipmentCancelledAsync(Guid companyId, Guid shipmentId)
        {
            ShipmentCancelledNotifications.Add((companyId, shipmentId));
            return Task.CompletedTask;
        }

        public Task NotifyShipmentAssignedAsync(Guid companyId, Guid customerId, Guid shipmentId, string driverName, string vehiclePlate)
        {
            ShipmentAssignedNotifications.Add((companyId, customerId, shipmentId, driverName, vehiclePlate));
            return Task.CompletedTask;
        }

        public Task NotifyDriverAvailabilityChangedAsync(Guid companyId, Guid driverId, bool isAvailable)
        {
            DriverAvailabilityNotifications.Add((companyId, driverId, isAvailable));
            return Task.CompletedTask;
        }
    }
}
