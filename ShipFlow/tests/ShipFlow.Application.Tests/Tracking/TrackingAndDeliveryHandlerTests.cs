using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Application.Features.Tracking;
using ShipFlow.Application.Tests.Dispatching;
using ShipFlow.Application.Tests.Shipments;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Tests.Tracking;

public class TrackingAndDeliveryHandlerTests
{
    private readonly FakeClock _time = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeShipmentStore _shipments = new();
    private readonly FakeAssignmentStore _assignments = new();
    private readonly FakeCompanyStore _companies = new();
    private readonly FakeFleetStore _fleet = new();
    private readonly FakeTrackingStore _tracking = new();
    private readonly FakeDriverAssignmentStore _driverAssignments = new();
    private readonly FakeCompanyAccess _companyAccess = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _carrierAdminId = Guid.NewGuid();
    private readonly Guid _driverUserId = Guid.NewGuid();
    private readonly TransportCompany _company;
    private readonly Driver _driver;
    private readonly Vehicle _vehicle;
    private readonly Shipment _shipment;
    private readonly Assignment _assignment;

    public TrackingAndDeliveryHandlerTests()
    {
        _company = TransportCompany.Create("Speedy Freight", _carrierAdminId, _time.Now);
        _company.AddMember(_driverUserId, CompanyRole.Driver, _time.Now);
        _driver = _company.RegisterDriver(_driverUserId, "Dave Driver", "DL-999");
        _vehicle = _company.RegisterVehicle("TRUCK-1", VehicleType.Truck, Capacity.Of(10_000m, 50m));

        _companies.AddAsync(_company, CancellationToken.None).Wait();
        _fleet.AddDriverAsync(_driver, CancellationToken.None).Wait();
        _fleet.AddVehicleAsync(_vehicle, CancellationToken.None).Wait();
        _companyAccess.Set(_driverUserId, _company.Id, CompanyRole.Driver);

        var stops = new[]
        {
            new StopInfo("Pickup", GeoPoint.Of(30.0, 31.0), TimeWindow.Of(_time.Now.AddHours(1), _time.Now.AddHours(3))),
            new StopInfo("Drop", GeoPoint.Of(30.5, 31.5))
        };
        _shipment = Shipment.Create(_customerId, Cargo.Of("Goods", 500m, 2m), stops, _time.Now, "REF-100");
        _shipment.OpenForQuoting(_customerId, _time.Now);
        var quote = _shipment.SubmitQuote(_company.Id, Money.Of(500m, "USD"), _time.Now.AddDays(1), "Net 30", _time.Now);
        _shipment.AcceptQuote(_customerId, quote.Id, _time.Now);

        var window = TimeWindow.Of(_time.Now.AddHours(1), _time.Now.AddHours(3));
        _assignment = Assignment.Create(_shipment, _driver, _vehicle, window, _carrierAdminId, [], _time.Now);
        _assignments.AddAsync(_assignment, CancellationToken.None).Wait();
        _driverAssignments.Assignments.Add(_assignment);
        _shipments.AddAsync(_shipment, CancellationToken.None).Wait();

        _currentUser.Set(_driverUserId, Roles.Driver);
    }

    [Fact]
    public async Task PickUp_ValidDriver_TransitionsToPickedUp()
    {
        var handler = new PickUpShipmentHandler(
            _currentUser, _companyAccess, _fleet, _shipments, _assignments, _unitOfWork, _time);

        var result = await handler.HandleAsync(_shipment.Id, CancellationToken.None);

        Assert.Equal(ShipmentStatus.PickedUp, result.Status);
        Assert.Equal(ShipmentStatus.PickedUp, _shipment.Status);
    }

    [Fact]
    public async Task PickUp_UnauthorizedDriver_ThrowsForbidden()
    {
        var otherUserId = Guid.NewGuid();
        _currentUser.Set(otherUserId, Roles.Driver);
        _companyAccess.Set(otherUserId, _company.Id, CompanyRole.Driver);
        _company.AddMember(otherUserId, CompanyRole.Driver, _time.Now);
        var otherDriver = _company.RegisterDriver(otherUserId, "Other Driver", "DL-888");
        await _fleet.AddDriverAsync(otherDriver, CancellationToken.None);

        var handler = new PickUpShipmentHandler(
            _currentUser, _companyAccess, _fleet, _shipments, _assignments, _unitOfWork, _time);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(_shipment.Id, CancellationToken.None));
    }

    [Fact]
    public async Task StartTransit_FromPickedUp_TransitionsToInTransit()
    {
        _shipment.PickUp(_driverUserId, _time.Now);

        var handler = new StartTransitHandler(
            _currentUser, _companyAccess, _fleet, _shipments, _assignments, _unitOfWork, _time);

        var result = await handler.HandleAsync(_shipment.Id, CancellationToken.None);

        Assert.Equal(ShipmentStatus.InTransit, result.Status);
        Assert.Equal(ShipmentStatus.InTransit, _shipment.Status);
    }

    [Fact]
    public async Task Deliver_CreatesProofAndCompletesAssignment()
    {
        _shipment.PickUp(_driverUserId, _time.Now);
        _shipment.StartTransit(_driverUserId, _time.Now);

        var validator = new DeliverShipmentCommandValidator();
        var handler = new DeliverShipmentHandler(
            validator, _currentUser, _companyAccess, _fleet, _shipments, _assignments, _tracking, _unitOfWork, _time);

        var command = new DeliverShipmentCommand(
            _time.Now, "John Recipient", "Left at front door", "sig-123", "photo-456");

        var result = await handler.HandleAsync(_shipment.Id, command, CancellationToken.None);

        Assert.Equal("John Recipient", result.RecipientName);
        Assert.Equal("Left at front door", result.Notes);
        Assert.Equal(ShipmentStatus.Delivered, _shipment.Status);
        Assert.Equal(AssignmentStatus.Completed, _assignment.Status);
        Assert.NotNull(await _tracking.FindProofAsync(_shipment.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Deliver_DuplicateProof_ThrowsConflict()
    {
        _shipment.PickUp(_driverUserId, _time.Now);
        _shipment.StartTransit(_driverUserId, _time.Now);

        var validator = new DeliverShipmentCommandValidator();
        var handler = new DeliverShipmentHandler(
            validator, _currentUser, _companyAccess, _fleet, _shipments, _assignments, _tracking, _unitOfWork, _time);

        var command = new DeliverShipmentCommand(_time.Now, "John", null, null, null);
        await handler.HandleAsync(_shipment.Id, command, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(_shipment.Id, command, CancellationToken.None));
    }

    [Fact]
    public async Task FailDelivery_TransitionsToDeliveryFailed_AndAssignmentRemainsActive()
    {
        _shipment.PickUp(_driverUserId, _time.Now);
        _shipment.StartTransit(_driverUserId, _time.Now);

        var validator = new FailDeliveryCommandValidator();
        var handler = new FailDeliveryHandler(
            validator, _currentUser, _companyAccess, _fleet, _shipments, _assignments, _unitOfWork, _time);

        var command = new FailDeliveryCommand("Recipient was not home");
        var result = await handler.HandleAsync(_shipment.Id, command, CancellationToken.None);

        Assert.Equal(ShipmentStatus.DeliveryFailed, result.Status);
        Assert.Equal(AssignmentStatus.Active, _assignment.Status);
    }

    [Fact]
    public async Task RetryDelivery_TransitionsFromDeliveryFailedBackToInTransit()
    {
        _shipment.PickUp(_driverUserId, _time.Now);
        _shipment.StartTransit(_driverUserId, _time.Now);
        _shipment.FailDelivery(_driverUserId, "Missed", _time.Now);

        var handler = new RetryDeliveryHandler(
            _currentUser, _companyAccess, _fleet, _shipments, _assignments, _unitOfWork, _time);

        var result = await handler.HandleAsync(_shipment.Id, CancellationToken.None);

        Assert.Equal(ShipmentStatus.InTransit, result.Status);
    }

    [Fact]
    public async Task SubmitTrackingPoint_SavesAndDeduplicates()
    {
        _shipment.PickUp(_driverUserId, _time.Now);
        _shipment.StartTransit(_driverUserId, _time.Now);

        var validator = new SubmitTrackingPointCommandValidator();
        var handler = new SubmitTrackingPointHandler(
            validator, _currentUser, _companyAccess, _fleet, _shipments, _assignments, _tracking, _unitOfWork, _time);

        var command = new SubmitTrackingPointCommand(_shipment.Id, 30.1, 31.2, _time.Now, 60m);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(30.1, result.Latitude);
        Assert.Equal(31.2, result.Longitude);
        Assert.Single(_tracking.Points);

        // Submitting duplicate point (same timestamp and user) is safely deduplicated
        var dupResult = await handler.HandleAsync(command, CancellationToken.None);
        Assert.Equal(result.Id, dupResult.Id);
        Assert.Single(_tracking.Points);
    }

    [Fact]
    public async Task GetCurrentDriverAssignment_ReturnsCurrentActiveAssignment()
    {
        var handler = new GetCurrentDriverAssignmentHandler(
            _currentUser, _companyAccess, _fleet, _driverAssignments, _shipments);

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(_assignment.Id, result.Id);
        Assert.Equal(_shipment.Id, result.ShipmentId);
    }
}
