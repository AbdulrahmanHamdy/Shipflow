using ShipFlow.Domain.Common;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tests.Support;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tests;

public class AssignmentEligibilityTests
{
    [Fact]
    public void Create_AssignsAndMovesShipmentToAssigned()
    {
        var f = new Fixture(ShipmentStatus.Accepted);

        var assignment = Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10));

        Assert.Equal(AssignmentStatus.Active, assignment.Status);
        Assert.Equal(f.Shipment.Id, assignment.ShipmentId);
        Assert.Equal(f.Company.Id, assignment.CompanyId);
        Assert.Equal(f.Driver.Id, assignment.DriverId);
        Assert.Equal(f.Vehicle.Id, assignment.VehicleId);
        Assert.Equal(ShipmentStatus.Assigned, f.Shipment.Status);
        Assert.Equal(assignment.Id, f.Shipment.CurrentAssignmentId);

        var evt = f.Shipment.Events[^1];
        Assert.Equal(ShipmentStatus.Accepted, evt.PreviousStatus);
        Assert.Equal(f.DispatcherUserId, evt.ActorId);
        Assert.Equal(assignment.Id.ToString(), evt.Metadata["assignmentId"]);
    }

    [Theory]
    [InlineData(ShipmentStatus.Pending)]
    [InlineData(ShipmentStatus.Quoting)]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void Create_RequiresAnAcceptedShipment(ShipmentStatus status)
    {
        var f = new Fixture(status);

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10)));
    }

    [Fact]
    public void Create_RejectsDriverFromAnotherCompany()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var other = Fixture.OtherCompany();

        DomainAssert.Fails(DomainErrorCodes.CrossCompany, () =>
            Assignment.Create(f.Shipment, other.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10)));
        Assert.Equal(ShipmentStatus.Accepted, f.Shipment.Status);
    }

    [Fact]
    public void Create_RejectsVehicleFromAnotherCompany()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var other = Fixture.OtherCompany();

        DomainAssert.Fails(DomainErrorCodes.CrossCompany, () =>
            Assignment.Create(f.Shipment, f.Driver, other.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10)));
    }

    [Fact]
    public void Create_RejectsInactiveDriverOrVehicle()
    {
        var f = new Fixture(ShipmentStatus.Accepted);

        f.Driver.Deactivate();
        DomainAssert.Fails(DomainErrorCodes.InvalidState, () =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10)));

        f.Driver.Activate();
        f.Vehicle.Deactivate();
        DomainAssert.Fails(DomainErrorCodes.InvalidState, () =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10)));
    }

    [Fact]
    public void Create_RejectsVehicleWithInsufficientWeightCapacity()
    {
        var f = new Fixture(ShipmentStatus.Accepted, cargoKg: 1000.5m, cargoM3: 1m);

        DomainAssert.Fails(DomainErrorCodes.CapacityExceeded, () =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10)));
    }

    [Fact]
    public void Create_RejectsVehicleWithInsufficientVolumeCapacity()
    {
        var f = new Fixture(ShipmentStatus.Accepted, cargoKg: 10m, cargoM3: 10.5m);

        DomainAssert.Fails(DomainErrorCodes.CapacityExceeded, () =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10)));
    }

    [Fact]
    public void Create_AcceptsCargoExactlyAtCapacity()
    {
        var f = new Fixture(ShipmentStatus.Accepted, cargoKg: 1000m, cargoM3: 10m);

        var assignment = Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [], Fixture.T(10));

        Assert.Equal(AssignmentStatus.Active, assignment.Status);
    }

    [Fact]
    public void Create_RejectsWindowThatAlreadyEnded()
    {
        var f = new Fixture(ShipmentStatus.Accepted);

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(10, 20), f.DispatcherUserId, [], Fixture.T(30)));
    }

    [Fact]
    public void Create_RejectsEmptyDispatcher()
    {
        var f = new Fixture(ShipmentStatus.Accepted);

        Assert.Throws<DomainException>(() =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), Guid.Empty, [], Fixture.T(10)));
    }
}

public class AssignmentConflictTests
{
    private static Assignment ActiveAssignment(Fixture f, int startMin, int endMin)
    {
        var other = new Fixture(ShipmentStatus.Accepted);
        // Reuse the same driver and vehicle identities by assigning on a second shipment of the same fixture company.
        var shipment = Shipment.Create(f.CustomerId, Cargo.Of("Other", 1m, 1m), Fixture.Stops(), Fixture.Now);
        shipment.OpenForQuoting(f.CustomerId, Fixture.T(1));
        var quote = shipment.SubmitQuote(f.Company.Id, Money.Of(10m, "USD"), Fixture.T(1000), "t", Fixture.T(1));
        shipment.AcceptQuote(f.CustomerId, quote.Id, Fixture.T(2));
        _ = other;
        return Assignment.Create(shipment, f.Driver, f.Vehicle, Fixture.Window(startMin, endMin), f.DispatcherUserId, [], Fixture.T(3));
    }

    [Fact]
    public void Create_RejectsOverlappingWindowForTheSameDriver()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var existing = ActiveAssignment(f, 120, 240);
        var anotherVehicle = f.Company.RegisterVehicle("XX 111 YY", VehicleType.Van, Capacity.Of(1000m, 10m));

        var ex = Assert.Throws<AssignmentConflictException>(() =>
            Assignment.Create(f.Shipment, f.Driver, anotherVehicle, Fixture.Window(200, 300), f.DispatcherUserId, [existing], Fixture.T(10)));

        Assert.Equal(DomainErrorCodes.Conflict, ex.Code);
        var conflict = Assert.Single(ex.Conflicts);
        Assert.Equal(ConflictResource.Driver, conflict.Resource);
        Assert.Equal(existing.Id, conflict.AssignmentId);
        Assert.Equal(ShipmentStatus.Accepted, f.Shipment.Status);
    }

    [Fact]
    public void Create_RejectsOverlappingWindowForTheSameVehicle()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var existing = ActiveAssignment(f, 120, 240);
        var driverUser = Guid.NewGuid();
        f.Company.AddMember(driverUser, Companies.CompanyRole.Driver, Fixture.Now);
        var anotherDriver = f.Company.RegisterDriver(driverUser, "Other Driver", "LIC-2");

        var ex = Assert.Throws<AssignmentConflictException>(() =>
            Assignment.Create(f.Shipment, anotherDriver, f.Vehicle, Fixture.Window(100, 130), f.DispatcherUserId, [existing], Fixture.T(10)));

        Assert.Equal(ConflictResource.Vehicle, Assert.Single(ex.Conflicts).Resource);
    }

    [Fact]
    public void Create_ReportsBothDriverAndVehicleConflicts()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var existing = ActiveAssignment(f, 120, 240);

        var ex = Assert.Throws<AssignmentConflictException>(() =>
            Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(130, 150), f.DispatcherUserId, [existing], Fixture.T(10)));

        Assert.Equal(2, ex.Conflicts.Count);
        Assert.Contains(ConflictResource.Driver, ex.Conflicts.Select(c => c.Resource));
        Assert.Contains(ConflictResource.Vehicle, ex.Conflicts.Select(c => c.Resource));
    }

    [Fact]
    public void Create_AllowsBackToBackWindows()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var existing = ActiveAssignment(f, 120, 240);

        var assignment = Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(240, 300), f.DispatcherUserId, [existing], Fixture.T(10));

        Assert.Equal(AssignmentStatus.Active, assignment.Status);
    }

    [Fact]
    public void Create_AllowsDisjointWindows()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var existing = ActiveAssignment(f, 120, 240);

        Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(500, 600), f.DispatcherUserId, [existing], Fixture.T(10));

        Assert.Equal(ShipmentStatus.Assigned, f.Shipment.Status);
    }

    [Fact]
    public void FindConflicts_IgnoresReleasedAndCompletedAssignments()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var released = ActiveAssignment(f, 120, 240);
        released.Release("changed plan", Fixture.T(5));

        var conflicts = Assignment.FindConflicts([released], f.Driver.Id, f.Vehicle.Id, Fixture.Window(130, 150));

        Assert.Empty(conflicts);
    }

    [Fact]
    public void FindConflicts_IgnoresOtherDriversAndVehicles()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var existing = ActiveAssignment(f, 120, 240);

        var conflicts = Assignment.FindConflicts([existing], Guid.NewGuid(), Guid.NewGuid(), Fixture.Window(130, 150));

        Assert.Empty(conflicts);
    }

    [Fact]
    public void FindConflicts_ReturnsEveryConflictingAssignment()
    {
        var f = new Fixture(ShipmentStatus.Accepted);
        var first = ActiveAssignment(f, 120, 180);
        var second = ActiveAssignment(f, 180, 240);

        var conflicts = Assignment.FindConflicts([first, second], f.Driver.Id, f.Vehicle.Id, Fixture.Window(150, 200));

        Assert.Equal(4, conflicts.Count);
    }
}

public class AssignmentLifecycleTests
{
    [Fact]
    public void UnassignFrom_ReleasesAssignment_ReturnsShipmentToAccepted_AndAllowsReassignment()
    {
        var f = new Fixture(ShipmentStatus.Assigned);
        var assignment = f.Assignment!;

        assignment.UnassignFrom(f.Shipment, f.DispatcherUserId, "driver sick", Fixture.T(20));

        Assert.Equal(AssignmentStatus.Released, assignment.Status);
        Assert.Equal("driver sick", assignment.ReleaseReason);
        Assert.Equal(ShipmentStatus.Accepted, f.Shipment.Status);
        Assert.Null(f.Shipment.CurrentAssignmentId);
        Assert.Equal("driver sick", f.Shipment.Events[^1].Metadata["reason"]);

        var replacement = Assignment.Create(f.Shipment, f.Driver, f.Vehicle, Fixture.Window(), f.DispatcherUserId, [assignment], Fixture.T(30));
        Assert.Equal(ShipmentStatus.Assigned, f.Shipment.Status);
        Assert.Equal(replacement.Id, f.Shipment.CurrentAssignmentId);
    }

    [Fact]
    public void UnassignFrom_RequiresReason()
    {
        var f = new Fixture(ShipmentStatus.Assigned);

        DomainAssert.Fails(DomainErrorCodes.Validation, () => f.Assignment!.UnassignFrom(f.Shipment, f.DispatcherUserId, " ", Fixture.T(20)));
        Assert.Equal(AssignmentStatus.Active, f.Assignment!.Status);
        Assert.Equal(ShipmentStatus.Assigned, f.Shipment.Status);
    }

    [Fact]
    public void UnassignFrom_AfterPickup_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.PickedUp);

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () =>
            f.Assignment!.UnassignFrom(f.Shipment, f.DispatcherUserId, "x", Fixture.T(150)));
        Assert.Equal(AssignmentStatus.Active, f.Assignment!.Status);
    }

    [Fact]
    public void UnassignFrom_AnotherShipmentsAssignment_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Assigned);
        var other = new Fixture(ShipmentStatus.Assigned);

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () =>
            other.Assignment!.UnassignFrom(f.Shipment, f.DispatcherUserId, "x", Fixture.T(20)));
    }

    [Fact]
    public void Release_AfterShipmentCancellation_EndsTheAssignment()
    {
        var f = new Fixture(ShipmentStatus.Assigned);
        f.Shipment.Cancel(CancellationInitiator.Customer, f.CustomerId, "x", Fixture.T(20));

        f.Assignment!.Release("shipment cancelled", Fixture.T(21));

        Assert.Equal(AssignmentStatus.Released, f.Assignment.Status);
        Assert.Equal(Fixture.T(21), f.Assignment.EndedAtUtc);
    }

    [Fact]
    public void Release_Twice_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Assigned);
        f.Assignment!.Release("x", Fixture.T(20));

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => f.Assignment.Release("y", Fixture.T(21)));
    }

    [Fact]
    public void Complete_RequiresDeliveredShipment()
    {
        var f = new Fixture(ShipmentStatus.InTransit);

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => f.Assignment!.Complete(f.Shipment, Fixture.T(220)));

        f.Shipment.Deliver(f.DriverUserId, Fixture.T(210));
        f.Assignment!.Complete(f.Shipment, Fixture.T(220));

        Assert.Equal(AssignmentStatus.Completed, f.Assignment.Status);
    }

    [Fact]
    public void CompletedAssignment_CannotBeReleased()
    {
        var f = new Fixture(ShipmentStatus.Delivered);
        f.Assignment!.Complete(f.Shipment, Fixture.T(220));

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => f.Assignment.Release("x", Fixture.T(230)));
    }
}
