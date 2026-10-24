using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Tests.Shipments;

public class ShipmentHandlerTests
{
    [Fact]
    public async Task Create_PersistsAPendingShipment_OwnedByTheCaller()
    {
        var w = new World();
        w.AsCustomer();

        var dto = await w.Create().HandleAsync(World.ValidCreate(), CancellationToken.None);

        var saved = Assert.Single(w.Store.Shipments.Values);
        Assert.Equal(w.Customer, saved.CustomerId);
        Assert.Equal(ShipmentStatus.Pending, saved.Status);
        Assert.Equal(saved.Id, dto.Id);
        Assert.Equal(2, dto.Stops.Count);
        Assert.Equal(1, w.Uow.Saves);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_IsForbidden()
    {
        var w = new World();
        w.User.Set(null);

        await Assert.ThrowsAsync<ForbiddenException>(() => w.Create().HandleAsync(World.ValidCreate(), CancellationToken.None));
        Assert.Empty(w.Store.Shipments);
    }

    [Fact]
    public async Task OpenForQuoting_ByTheOwner_MovesToQuoting()
    {
        var w = new World();
        var shipment = w.AddShipment();
        w.AsCustomer();

        var dto = await w.Open().HandleAsync(shipment.Id, CancellationToken.None);

        Assert.Equal("Quoting", dto.Status);
        Assert.Equal(ShipmentStatus.Quoting, shipment.Status);
    }

    [Fact]
    public async Task OpenForQuoting_ByAnotherCustomer_LooksLikeNotFound_AndChangesNothing()
    {
        var w = new World();
        var shipment = w.AddShipment();
        w.AsCustomer(w.OtherCustomer);

        await Assert.ThrowsAsync<NotFoundException>(() => w.Open().HandleAsync(shipment.Id, CancellationToken.None));
        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        Assert.Equal(0, w.Uow.Saves);
    }

    [Fact]
    public async Task GetById_Owner_Admin_AndCompanyOnAnOpenShipment_CanView()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);

        w.AsCustomer();
        Assert.Equal(shipment.Id, (await w.GetById().HandleAsync(shipment.Id, CancellationToken.None)).Id);
        w.AsAdmin();
        Assert.Equal(shipment.Id, (await w.GetById().HandleAsync(shipment.Id, CancellationToken.None)).Id);
        w.AsDispatcherA();
        Assert.Equal(shipment.Id, (await w.GetById().HandleAsync(shipment.Id, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task GetById_AnotherCustomer_AndACompanyOnAPendingShipment_GetNotFound()
    {
        var w = new World();
        var pending = w.AddShipment();

        w.AsCustomer(w.OtherCustomer);
        await Assert.ThrowsAsync<NotFoundException>(() => w.GetById().HandleAsync(pending.Id, CancellationToken.None));
        w.AsDispatcherA();
        await Assert.ThrowsAsync<NotFoundException>(() => w.GetById().HandleAsync(pending.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => w.GetById().HandleAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_AfterAcceptance_OnlyTheCarrierCompanyKeepsAccess()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        var quote = w.AddQuote(shipment, w.CompanyA);
        shipment.AcceptQuote(w.Customer, quote.Id, w.Time.Now);

        w.AsDispatcherA();
        Assert.Equal(w.CompanyA.Id, (await w.GetById().HandleAsync(shipment.Id, CancellationToken.None)).CarrierCompanyId);
        w.AsDispatcherB();
        await Assert.ThrowsAsync<NotFoundException>(() => w.GetById().HandleAsync(shipment.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetById_OwnerSeesAllQuotes_EachCompanySeesOnlyItsOwn()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AddQuote(shipment, w.CompanyA, 100m);
        w.AddQuote(shipment, w.CompanyB, 90m);

        w.AsCustomer();
        Assert.Equal(2, (await w.GetById().HandleAsync(shipment.Id, CancellationToken.None)).Quotes.Count);
        w.AsDispatcherA();
        var seenByA = (await w.GetById().HandleAsync(shipment.Id, CancellationToken.None)).Quotes;
        Assert.Equal(w.CompanyA.Id, Assert.Single(seenByA).CompanyId);
        w.AsDispatcherB();
        Assert.Equal(w.CompanyB.Id, Assert.Single((await w.GetById().HandleAsync(shipment.Id, CancellationToken.None)).Quotes).CompanyId);
    }

    [Fact]
    public async Task List_ScopeFollowsTheCallersIdentity()
    {
        var w = new World();

        w.AsCustomer();
        await w.List().HandleAsync(new GetShipmentsQuery(), CancellationToken.None);
        Assert.Equal(w.Customer, w.Store.LastFilter!.CustomerId);
        Assert.Null(w.Store.LastFilter.CarrierCompanyId);

        w.AsDispatcherA();
        await w.List().HandleAsync(new GetShipmentsQuery(), CancellationToken.None);
        Assert.Equal(w.CompanyA.Id, w.Store.LastFilter!.CarrierCompanyId);
        Assert.Null(w.Store.LastFilter.CustomerId);

        w.AsAdmin();
        await w.List().HandleAsync(new GetShipmentsQuery(), CancellationToken.None);
        Assert.Null(w.Store.LastFilter!.CustomerId);
        Assert.Null(w.Store.LastFilter.CarrierCompanyId);
        Assert.False(w.Store.LastFilter.OpenForQuotingOnly);
    }

    [Fact]
    public async Task List_OpenScope_IsForCompaniesOnly_AndPassesFiltersThrough()
    {
        var w = new World();
        w.AsCustomer();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            w.List().HandleAsync(new GetShipmentsQuery(ShipmentScope.OpenForQuotes), CancellationToken.None));

        w.AsDispatcherA();
        await w.List().HandleAsync(
            new GetShipmentsQuery(ShipmentScope.OpenForQuotes, [ShipmentStatus.Quoting], Search: "  tiles ", Page: 3, PageSize: 10),
            CancellationToken.None);

        var filter = w.Store.LastFilter!;
        Assert.True(filter.OpenForQuotingOnly);
        Assert.Equal("tiles", filter.Search);
        Assert.Equal(3, filter.Page);
        Assert.Equal(10, filter.PageSize);
        Assert.Equal(new[] { ShipmentStatus.Quoting }, filter.Statuses.ToArray());
    }

    [Fact]
    public async Task Cancel_ByTheOwner_CancelsAndRecordsTheReason()
    {
        var w = new World();
        var shipment = w.AddShipment();
        w.AsCustomer();

        var dto = await w.Cancel().HandleAsync(shipment.Id, new CancelShipmentCommand("changed plans"), CancellationToken.None);

        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("changed plans", shipment.Events.Last().Metadata["reason"]);
        Assert.Equal(1, w.Uow.Saves);
    }

    [Fact]
    public async Task Cancel_ByAStranger_IsNotFound()
    {
        var w = new World();
        var shipment = w.AddShipment();
        w.AsCustomer(w.OtherCustomer);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            w.Cancel().HandleAsync(shipment.Id, new CancelShipmentCommand("x"), CancellationToken.None));
        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
    }

    [Fact]
    public async Task Cancel_ByACompanyThatIsNotTheCarrier_IsForbidden()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AsDispatcherA();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            w.Cancel().HandleAsync(shipment.Id, new CancelShipmentCommand("x"), CancellationToken.None));
        Assert.Equal(ShipmentStatus.Quoting, shipment.Status);
    }

    [Fact]
    public async Task Cancel_ByTheCarrier_UsesTheCompanyIdFromMembership_AndReleasesTheAssignment()
    {
        var w = new World();
        var (shipment, assignment) = w.AddAssignedShipment();
        w.AsDispatcherA();

        await w.Cancel().HandleAsync(shipment.Id, new CancelShipmentCommand("vehicle broke down"), CancellationToken.None);

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
        Assert.Equal(w.CompanyA.Id, shipment.Events.Last().ActorId);
        Assert.Equal(AssignmentStatus.Released, assignment.Status);
        Assert.Equal(1, w.Uow.Saves);
    }

    [Fact]
    public async Task Cancel_ByTheCustomerOfAnAssignedShipment_ReleasesTheDriverAndVehicle()
    {
        var w = new World();
        var (shipment, assignment) = w.AddAssignedShipment();
        w.AsCustomer();

        await w.Cancel().HandleAsync(shipment.Id, new CancelShipmentCommand("not needed"), CancellationToken.None);

        Assert.Equal(AssignmentStatus.Released, assignment.Status);
        Assert.Null(shipment.CurrentAssignmentId);
    }

    [Fact]
    public async Task Cancel_ByTheCustomerAfterPickup_IsRejectedByTheLifecycle_AndLeavesTheAssignment()
    {
        var w = new World();
        var (shipment, assignment) = w.AddAssignedShipment();
        shipment.PickUp(w.DriverA, w.Time.Now);
        w.AsCustomer();

        await Assert.ThrowsAsync<DomainException>(() =>
            w.Cancel().HandleAsync(shipment.Id, new CancelShipmentCommand("too late"), CancellationToken.None));

        Assert.Equal(AssignmentStatus.Active, assignment.Status);
        Assert.Equal(0, w.Uow.Saves);
    }

    [Fact]
    public async Task Cancel_ByPlatformAdmin_IsAllowed()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AsAdmin();

        await w.Cancel().HandleAsync(shipment.Id, new CancelShipmentCommand("fraud"), CancellationToken.None);

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
    }
}
