using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tests.Support;

namespace ShipFlow.Domain.Tests;

public class ShipmentProgressTests
{
    [Fact]
    public void HappyPath_RecordsEveryTransitionWithActorTimeAndPreviousState()
    {
        var f = new Fixture(ShipmentStatus.Delivered);

        var transitions = f.Shipment.Events.Select(e => (e.PreviousStatus, e.NewStatus)).ToArray();

        Assert.Equal(
            new (ShipmentStatus?, ShipmentStatus)[]
            {
                (null, ShipmentStatus.Pending),
                (ShipmentStatus.Pending, ShipmentStatus.Quoting),
                (ShipmentStatus.Quoting, ShipmentStatus.Accepted),
                (ShipmentStatus.Accepted, ShipmentStatus.Assigned),
                (ShipmentStatus.Assigned, ShipmentStatus.PickedUp),
                (ShipmentStatus.PickedUp, ShipmentStatus.InTransit),
                (ShipmentStatus.InTransit, ShipmentStatus.Delivered)
            },
            transitions);

        Assert.All(f.Shipment.Events, e => Assert.NotEqual(Guid.Empty, e.ActorId));
        Assert.All(f.Shipment.Events, e => Assert.Equal(DateTimeKind.Utc, e.OccurredAtUtc.Kind));
        Assert.Equal(f.DriverUserId, f.Shipment.Events[^1].ActorId);
        Assert.Equal(ShipmentStatus.Delivered, f.Shipment.Status);
    }

    [Fact]
    public void Events_AreInChronologicalOrder()
    {
        var f = new Fixture(ShipmentStatus.Delivered);

        var times = f.Shipment.Events.Select(e => e.OccurredAtUtc).ToArray();

        Assert.Equal(times.OrderBy(t => t).ToArray(), times);
    }

    [Theory]
    [InlineData(ShipmentStatus.Pending)]
    [InlineData(ShipmentStatus.Quoting)]
    [InlineData(ShipmentStatus.Accepted)]
    public void PickUp_BeforeAssignment_IsAnInvalidTransition(ShipmentStatus status)
    {
        var f = new Fixture(status);

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.PickUp(f.DriverUserId, Fixture.T(130)));
        Assert.Equal(status, f.Shipment.Status);
    }

    [Theory]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.PickedUp)]
    public void Deliver_BeforeInTransit_IsAnInvalidTransition(ShipmentStatus status)
    {
        var f = new Fixture(status);

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.Deliver(f.DriverUserId, Fixture.T(300)));
    }

    [Fact]
    public void StartTransit_FromAssigned_IsAnInvalidTransition()
    {
        var f = new Fixture(ShipmentStatus.Assigned);

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.StartTransit(f.DriverUserId, Fixture.T(130)));
    }

    [Theory]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void TerminalShipments_RejectEveryFurtherTransition(ShipmentStatus terminal)
    {
        var f = new Fixture(terminal);
        var driver = f.DriverUserId;

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.OpenForQuoting(f.CustomerId, Fixture.T(400)));
        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.PickUp(driver, Fixture.T(400)));
        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.StartTransit(driver, Fixture.T(400)));
        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.Deliver(driver, Fixture.T(400)));
        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.FailDelivery(driver, "x", Fixture.T(400)));
        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.RetryDelivery(driver, Fixture.T(400)));
        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () =>
            f.Shipment.Cancel(CancellationInitiator.Platform, Guid.NewGuid(), "x", Fixture.T(400)));
    }

    [Theory]
    [InlineData(ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit)]
    public void FailDelivery_RecordsReasonAndMovesToDeliveryFailed(ShipmentStatus from)
    {
        var f = new Fixture(from);

        f.Shipment.FailDelivery(f.DriverUserId, "  recipient absent ", Fixture.T(200));

        Assert.Equal(ShipmentStatus.DeliveryFailed, f.Shipment.Status);
        var evt = f.Shipment.Events[^1];
        Assert.Equal(from, evt.PreviousStatus);
        Assert.Equal("recipient absent", evt.Metadata["reason"]);
    }

    [Fact]
    public void FailDelivery_RequiresReason()
    {
        var f = new Fixture(ShipmentStatus.InTransit);

        DomainAssert.Fails(DomainErrorCodes.Validation, () => f.Shipment.FailDelivery(f.DriverUserId, " ", Fixture.T(200)));
        Assert.Equal(ShipmentStatus.InTransit, f.Shipment.Status);
    }

    [Fact]
    public void FailDelivery_FromAssigned_IsAnInvalidTransition()
    {
        var f = new Fixture(ShipmentStatus.Assigned);

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.FailDelivery(f.DriverUserId, "x", Fixture.T(200)));
    }

    [Fact]
    public void RetryDelivery_AfterFailure_ReturnsToInTransit_ThenCanBeDelivered()
    {
        var f = new Fixture(ShipmentStatus.DeliveryFailed);

        f.Shipment.RetryDelivery(f.DriverUserId, Fixture.T(220));
        f.Shipment.Deliver(f.DriverUserId, Fixture.T(230));

        Assert.Equal(ShipmentStatus.Delivered, f.Shipment.Status);
    }

    [Fact]
    public void RetryDelivery_WhenNotFailed_IsAnInvalidTransition()
    {
        var f = new Fixture(ShipmentStatus.PickedUp);

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.RetryDelivery(f.DriverUserId, Fixture.T(220)));
        Assert.Equal(ShipmentStatus.PickedUp, f.Shipment.Status);
    }

    [Fact]
    public void StartTransit_AfterFailedDelivery_IsRejected_BecauseRetryDeliveryIsTheOnlyWayBack()
    {
        var f = new Fixture(ShipmentStatus.DeliveryFailed);
        var eventsBefore = f.Shipment.Events.Count;

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.StartTransit(f.DriverUserId, Fixture.T(220)));

        Assert.Equal(ShipmentStatus.DeliveryFailed, f.Shipment.Status);
        Assert.Equal(eventsBefore, f.Shipment.Events.Count);
    }
}

public class ShipmentCancellationTests
{
    [Theory]
    [InlineData(ShipmentStatus.Pending)]
    [InlineData(ShipmentStatus.Quoting)]
    [InlineData(ShipmentStatus.Accepted)]
    [InlineData(ShipmentStatus.Assigned)]
    public void Customer_CanCancel_BeforePickup(ShipmentStatus from)
    {
        var f = new Fixture(from);

        f.Shipment.Cancel(CancellationInitiator.Customer, f.CustomerId, "no longer needed", Fixture.T(20));

        Assert.Equal(ShipmentStatus.Cancelled, f.Shipment.Status);
        var evt = f.Shipment.Events[^1];
        Assert.Equal(from, evt.PreviousStatus);
        Assert.Equal(f.CustomerId, evt.ActorId);
        Assert.Equal("no longer needed", evt.Metadata["reason"]);
        Assert.Equal("Customer", evt.Metadata["initiator"]);
    }

    [Theory]
    [InlineData(ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.DeliveryFailed)]
    [InlineData(ShipmentStatus.Delivered)]
    public void Customer_CannotCancel_AfterPickup(ShipmentStatus from)
    {
        var f = new Fixture(from);

        Assert.Throws<DomainException>(() =>
            f.Shipment.Cancel(CancellationInitiator.Customer, f.CustomerId, "too late", Fixture.T(300)));
        Assert.Equal(from, f.Shipment.Status);
    }

    [Fact]
    public void Customer_CancelByNonOwner_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        DomainAssert.Fails(DomainErrorCodes.NotOwner, () =>
            f.Shipment.Cancel(CancellationInitiator.Customer, Guid.NewGuid(), "x", Fixture.T(20)));
        Assert.Equal(ShipmentStatus.Quoting, f.Shipment.Status);
    }

    [Fact]
    public void Cancel_RequiresReason()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            f.Shipment.Cancel(CancellationInitiator.Customer, f.CustomerId, "", Fixture.T(20)));
    }

    [Theory]
    [InlineData(ShipmentStatus.Accepted)]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.DeliveryFailed)]
    public void Carrier_CanCancel_WhenItIsTheAcceptedCarrier(ShipmentStatus from)
    {
        var f = new Fixture(from);

        f.Shipment.Cancel(CancellationInitiator.Carrier, f.Company.Id, "vehicle breakdown", Fixture.T(300));

        Assert.Equal(ShipmentStatus.Cancelled, f.Shipment.Status);
        Assert.Equal(f.Company.Id, f.Shipment.Events[^1].ActorId);
    }

    [Fact]
    public void Carrier_ThatIsNotTheAcceptedCarrier_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Accepted);

        DomainAssert.Fails(DomainErrorCodes.NotOwner, () =>
            f.Shipment.Cancel(CancellationInitiator.Carrier, Guid.NewGuid(), "x", Fixture.T(20)));
    }

    [Fact]
    public void Carrier_CannotCancel_BeforeAQuoteIsAccepted()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        DomainAssert.Fails(DomainErrorCodes.NotOwner, () =>
            f.Shipment.Cancel(CancellationInitiator.Carrier, f.Company.Id, "x", Fixture.T(20)));
    }

    [Fact]
    public void Carrier_CannotCancel_WhileInTransit()
    {
        var f = new Fixture(ShipmentStatus.InTransit);

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () =>
            f.Shipment.Cancel(CancellationInitiator.Carrier, f.Company.Id, "x", Fixture.T(300)));
    }

    [Fact]
    public void Platform_CanCancelAcceptedShipment_ButNotOneAlreadyPickedUp()
    {
        var accepted = new Fixture(ShipmentStatus.Accepted);
        accepted.Shipment.Cancel(CancellationInitiator.Platform, Guid.NewGuid(), "fraud review", Fixture.T(20));
        Assert.Equal(ShipmentStatus.Cancelled, accepted.Shipment.Status);

        var pickedUp = new Fixture(ShipmentStatus.PickedUp);
        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () =>
            pickedUp.Shipment.Cancel(CancellationInitiator.Platform, Guid.NewGuid(), "x", Fixture.T(300)));
    }

    [Fact]
    public void Cancel_RejectsOpenQuotes()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var a = f.SubmitQuote(Guid.NewGuid());
        var b = f.SubmitQuote(Guid.NewGuid());

        f.Shipment.Cancel(CancellationInitiator.Customer, f.CustomerId, "x", Fixture.T(20));

        Assert.Equal(QuoteStatus.Rejected, a.Status);
        Assert.Equal(QuoteStatus.Rejected, b.Status);
    }

    [Fact]
    public void Cancel_WhileAssigned_ClearsCurrentAssignment_AndRecordsItsIdForRelease()
    {
        var f = new Fixture(ShipmentStatus.Assigned);
        var assignmentId = f.Assignment!.Id;

        f.Shipment.Cancel(CancellationInitiator.Customer, f.CustomerId, "x", Fixture.T(20));

        Assert.Null(f.Shipment.CurrentAssignmentId);
        Assert.Equal(assignmentId.ToString(), f.Shipment.Events[^1].Metadata["assignmentId"]);
    }
}
