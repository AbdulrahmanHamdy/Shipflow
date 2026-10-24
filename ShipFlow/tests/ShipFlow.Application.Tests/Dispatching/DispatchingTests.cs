using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Tests.Dispatching;

public class DispatchingTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Assign_BooksDriverAndVehicle_AndMovesTheShipmentToAssigned()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();

        var dto = await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);

        Assert.Equal("Active", dto.Status);
        Assert.Equal("Assigned", dto.ShipmentStatus);
        Assert.Equal(w.A.Id, dto.CompanyId);
        Assert.Equal(w.W.DispatcherA, dto.AssignedByUserId);
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        Assert.Single(w.W.Assignments.Assignments);
        Assert.Equal(1, w.W.Uow.Saves);
    }

    [Fact]
    public async Task Assign_ByADriverOrCustomer_IsForbidden()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.User.Set(w.DriverUser, Roles.Driver);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct));
        w.W.AsCustomer();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct));
        Assert.Empty(w.W.Assignments.Assignments);
    }

    [Fact]
    public async Task Assign_ByAnotherCompany_LooksLikeANotFoundShipment()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherB();

        await Assert.ThrowsAsync<NotFoundException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct));
        Assert.Equal(ShipmentStatus.Accepted, shipment.Status);
    }

    [Fact]
    public async Task Assign_WithAnotherCompanysDriverOrVehicle_IsNotFound()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        var otherDriver = w.RegisterDriver(w.B, Guid.NewGuid(), "Olly", "LIC-9");
        var otherTruck = w.AddVehicle(w.B, "OTH 1", 9000m, 90m);
        w.W.AsDispatcherA();

        await Assert.ThrowsAsync<NotFoundException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(otherDriver, w.Truck, w.Window()), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, otherTruck, w.Window()), Ct));
        Assert.Empty(w.W.Assignments.Assignments);
    }

    [Fact]
    public async Task Assign_WhenTheCargoDoesNotFit_IsCapacityExceeded()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        var small = w.AddVehicle(w.A, "SML 1", 100m, 1m);
        w.W.AsDispatcherA();

        var ex = await Assert.ThrowsAsync<DomainException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, small, w.Window()), Ct));

        Assert.Equal(DomainErrorCodes.CapacityExceeded, ex.Code);
        Assert.Equal(ShipmentStatus.Accepted, shipment.Status);
    }

    [Fact]
    public async Task Assign_InactiveDriverOrVehicle_IsRejected()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        w.Truck.Deactivate();
        var ex = await Assert.ThrowsAsync<DomainException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct));
        Assert.Equal(DomainErrorCodes.InvalidState, ex.Code);
        w.Truck.Activate();
        w.Driver.Deactivate();
        ex = await Assert.ThrowsAsync<DomainException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct));
        Assert.Equal(DomainErrorCodes.InvalidState, ex.Code);
    }

    [Fact]
    public async Task Assign_ShipmentThatIsNotAccepted_IsRejected()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        var other = w.AddVehicle(w.A, "TRK 2", 5000m, 50m);

        var ex = await Assert.ThrowsAsync<DomainException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, other, w.Window(100)), Ct));
        Assert.Equal(DomainErrorCodes.InvalidState, ex.Code);
    }

    [Fact]
    public async Task Assign_OverlappingDriverOrVehicle_IsAConflict_ButBackToBackIsAllowed()
    {
        var w = new DispatchWorld();
        var first = w.AcceptedShipment();
        var second = w.AcceptedShipment();
        var third = w.AcceptedShipment();
        var truck2 = w.AddVehicle(w.A, "TRK 2", 5000m, 50m);
        var driver2 = w.RegisterDriver(w.A, Guid.NewGuid(), "Eve", "LIC-2");
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(first.Id, w.Cmd(w.Driver, w.Truck, w.Window(24, 4)), Ct);

        var sameDriver = await Assert.ThrowsAsync<AssignmentConflictException>(
            () => w.Assign().HandleAsync(second.Id, w.Cmd(w.Driver, truck2, w.Window(26, 4)), Ct));
        Assert.Equal(DomainErrorCodes.Conflict, sameDriver.Code);
        await Assert.ThrowsAsync<AssignmentConflictException>(
            () => w.Assign().HandleAsync(second.Id, w.Cmd(driver2, w.Truck, w.Window(26, 4)), Ct));

        // The windows are half-open: starting exactly when the first one ends does not conflict.
        var backToBack = await w.Assign().HandleAsync(third.Id, w.Cmd(w.Driver, w.Truck, w.Window(28, 4)), Ct);
        Assert.Equal("Active", backToBack.Status);
    }

    [Fact]
    public async Task Assign_WithAWindowEndingInThePast_IsRejected()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();

        await Assert.ThrowsAsync<DomainException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window(-10, 2)), Ct));
    }

    [Fact]
    public async Task Assign_InvalidWindow_FailsValidation()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        var start = w.Now.AddHours(24);

        await Assert.ThrowsAsync<ValidationFailedException>(() => w.Assign().HandleAsync(shipment.Id, new AssignShipmentCommand(w.Driver.Id, w.Truck.Id, start, start), Ct));
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.Assign().HandleAsync(shipment.Id, new AssignShipmentCommand(w.Driver.Id, w.Truck.Id, start, start.AddDays(30)), Ct));
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.Assign().HandleAsync(shipment.Id, new AssignShipmentCommand(Guid.Empty, w.Truck.Id, start, start.AddHours(1)), Ct));
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.Assign().HandleAsync(
            shipment.Id, new AssignShipmentCommand(w.Driver.Id, w.Truck.Id, DateTime.SpecifyKind(start, DateTimeKind.Unspecified), start.AddHours(1)), Ct));
    }

    [Fact]
    public async Task Unassign_ReleasesTheAssignment_AndAllowsReassignment()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);

        var details = await w.Unassign().HandleAsync(shipment.Id, new UnassignShipmentCommand("wrong truck"), Ct);

        Assert.Equal("Accepted", details.Status);
        Assert.Equal(AssignmentStatus.Released, w.W.Assignments.Assignments[0].Status);
        var again = await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        Assert.Equal("Active", again.Status);
        Assert.Equal(2, w.W.Assignments.Assignments.Count);
    }

    [Fact]
    public async Task Unassign_Rules_NoAssignment_BlankReason_OtherCompany()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await Assert.ThrowsAsync<NotFoundException>(() => w.Unassign().HandleAsync(shipment.Id, new UnassignShipmentCommand("x"), Ct));
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.Unassign().HandleAsync(shipment.Id, new UnassignShipmentCommand(" "), Ct));
        w.W.AsDispatcherB();
        await Assert.ThrowsAsync<NotFoundException>(() => w.Unassign().HandleAsync(shipment.Id, new UnassignShipmentCommand("x"), Ct));
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
    }

    [Fact]
    public async Task Unassign_AfterPickup_IsRejectedAndKeepsTheAssignment()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        shipment.PickUp(w.DriverUser, w.Now);

        await Assert.ThrowsAsync<DomainException>(() => w.Unassign().HandleAsync(shipment.Id, new UnassignShipmentCommand("x"), Ct));
        Assert.Equal(AssignmentStatus.Active, w.W.Assignments.Assignments[0].Status);
    }

    [Fact]
    public async Task AvailableVehicles_FiltersByCompanyActiveCapacityAndWindow()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        var other = w.AcceptedShipment();
        var free = w.AddVehicle(w.A, "AAA 1", 5000m, 50m);
        w.AddVehicle(w.A, "SML 1", 100m, 1m);
        w.AddVehicle(w.A, "OFF 1", 5000m, 50m).Deactivate();
        w.AddVehicle(w.B, "OTH 1", 9000m, 90m);
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(other.Id, w.Cmd(w.Driver, w.Truck, w.Window(24, 4)), Ct);
        var query = new AvailabilityQuery(w.Window(26, 2).StartUtc, w.Window(26, 2).EndUtc);

        var overlapping = await w.Vehicles().HandleAsync(shipment.Id, query, Ct);
        Assert.Equal(new[] { free.Id }, overlapping.Select(v => v.Id).ToArray());

        var backToBack = await w.Vehicles().HandleAsync(shipment.Id, new AvailabilityQuery(w.Window(28, 2).StartUtc, w.Window(28, 2).EndUtc), Ct);
        Assert.Equal(new[] { free.Id, w.Truck.Id }.OrderBy(i => i).ToArray(), backToBack.Select(v => v.Id).OrderBy(i => i).ToArray());
    }

    [Fact]
    public async Task AvailableDrivers_ExcludeBookedAndInactive_AndOtherCompaniesAreHidden()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        var other = w.AcceptedShipment();
        var free = w.RegisterDriver(w.A, Guid.NewGuid(), "Eve", "LIC-2");
        w.RegisterDriver(w.A, Guid.NewGuid(), "Off", "LIC-3").Deactivate();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(other.Id, w.Cmd(w.Driver, w.Truck, w.Window(24, 4)), Ct);
        var query = new AvailabilityQuery(w.Window(25, 2).StartUtc, w.Window(25, 2).EndUtc);

        var drivers = await w.Drivers().HandleAsync(shipment.Id, query, Ct);
        Assert.Equal(new[] { free.Id }, drivers.Select(d => d.Id).ToArray());

        w.W.AsDispatcherB();
        await Assert.ThrowsAsync<NotFoundException>(() => w.Drivers().HandleAsync(shipment.Id, query, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => w.Vehicles().HandleAsync(shipment.Id, query, Ct));
    }

    [Fact]
    public async Task Availability_RequiresAnAcceptedShipment_AndAValidWindow()
    {
        var w = new DispatchWorld();
        var pending = w.W.AddShipment();
        w.W.AsDispatcherA();
        var q = new AvailabilityQuery(w.Window().StartUtc, w.Window().EndUtc);
        await Assert.ThrowsAsync<NotFoundException>(() => w.Vehicles().HandleAsync(pending.Id, q, Ct));
        var accepted = w.AcceptedShipment();
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.Vehicles().HandleAsync(accepted.Id, new AvailabilityQuery(q.WindowEndUtc, q.WindowStartUtc), Ct));
        await w.Assign().HandleAsync(accepted.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        await Assert.ThrowsAsync<DomainException>(() => w.Vehicles().HandleAsync(accepted.Id, q, Ct));
    }

    [Fact]
    public async Task CancelAfterAssigning_ReleasesTheBooking_SoTheDriverIsFreeAgain()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);

        await w.W.Cancel().HandleAsync(shipment.Id, new ShipFlow.Application.Features.Shipments.CancelShipmentCommand("customer changed mind"), Ct);

        Assert.Equal(AssignmentStatus.Released, w.W.Assignments.Assignments[0].Status);
        var next = w.AcceptedShipment();
        var ok = await w.Assign().HandleAsync(next.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        Assert.Equal("Active", ok.Status);
    }

    // ------------------------------------------------------------------ the race

    private static async Task<(int Won, int Conflicts)> Race(DispatchWorld w, int n, bool sameDriver, bool sameVehicle)
    {
        w.W.AsDispatcherA();
        w.W.Assignments.YieldOnRead = true;
        var window = w.Window();
        var jobs = new List<Func<Task>>();
        var shipments = Enumerable.Range(0, n).Select(_ => w.AcceptedShipment()).ToList();
        var drivers = Enumerable.Range(0, n).Select(i => sameDriver ? w.Driver : w.RegisterDriver(w.A, Guid.NewGuid(), "D" + i, "L" + i)).ToList();
        var vehicles = Enumerable.Range(0, n).Select(i => sameVehicle ? w.Truck : w.AddVehicle(w.A, "V" + i, 5000m, 50m)).ToList();

        var tasks = Enumerable.Range(0, n).Select(i => Task.Run(async () =>
        {
            try
            {
                await w.Assign().HandleAsync(shipments[i].Id, w.Cmd(drivers[i], vehicles[i], window), Ct);
                return true;
            }
            catch (AssignmentConflictException)
            {
                return false;
            }
        })).ToList();
        var results = await Task.WhenAll(tasks);
        _ = jobs;
        return (results.Count(r => r), results.Count(r => !r));
    }

    [Fact]
    public async Task Race_EightRequestsForTheSameDriver_ExactlyOneWins_WhenLocksAreHeld()
    {
        var w = new DispatchWorld();
        var (won, conflicts) = await Race(w, 8, sameDriver: true, sameVehicle: false);
        Assert.Equal(1, won);
        Assert.Equal(7, conflicts);
        Assert.Single(w.W.Assignments.Assignments);
    }

    [Fact]
    public async Task Race_EightRequestsForTheSameVehicle_ExactlyOneWins_WhenLocksAreHeld()
    {
        var w = new DispatchWorld();
        var (won, conflicts) = await Race(w, 8, sameDriver: false, sameVehicle: true);
        Assert.Equal(1, won);
        Assert.Equal(7, conflicts);
    }

    [Fact]
    public async Task Race_IndependentResources_AllSucceed()
    {
        var w = new DispatchWorld();
        var (won, _) = await Race(w, 6, sameDriver: false, sameVehicle: false);
        Assert.Equal(6, won);
    }

    [Fact]
    public async Task Race_Control_WithoutLocksSeveralRequestsDoubleBook()
    {
        // Proves the scenario is a real race: the same code with the lock switched off lets more than one request through.
        var w = new DispatchWorld();
        w.Tx.Enabled = false;
        var (won, _) = await Race(w, 8, sameDriver: true, sameVehicle: false);
        Assert.True(won > 1, $"Expected the unlocked control run to double-book, but {won} request(s) won.");
    }
}
