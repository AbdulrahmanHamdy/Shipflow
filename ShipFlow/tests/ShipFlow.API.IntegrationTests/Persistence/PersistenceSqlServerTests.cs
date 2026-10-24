using Microsoft.EntityFrameworkCore;
using ShipFlow.API.IntegrationTests.Support;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;
using ShipFlow.Domain.ValueObjects;
using static ShipFlow.API.IntegrationTests.Support.PersistenceData;

namespace ShipFlow.API.IntegrationTests.Persistence;

/// <summary>
/// Mapping, constraints and concurrency tokens verified against real SQL Server (the EF in-memory provider cannot
/// honour any of them). Skipped, not passed, when SHIPFLOW_TEST_SQLSERVER is not set.
/// </summary>
public class PersistenceSqlServerTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _db;

    public PersistenceSqlServerTests(SqlServerDatabaseFixture db) => _db = db;

    private async Task<(Guid Customer, TransportCompany A, TransportCompany B, Shipment Shipment)> SeedAcceptedShipment()
    {
        var customer = NewUser("customer");
        var (companyA, adminA) = NewCompany("Carrier A");
        var (companyB, adminB) = NewCompany("Carrier B");
        var shipment = NewShipment(customer.Id);
        shipment.OpenForQuoting(customer.Id, Now);
        var q1 = shipment.SubmitQuote(companyA.Id, Money.Of(1500.1234m, "usd"), Now.AddHours(2), "Net 30", Now);
        shipment.SubmitQuote(companyB.Id, Money.Of(1400m, "EUR"), Now.AddHours(2), "Prepaid", Now);
        shipment.AcceptQuote(customer.Id, q1.Id, Now.AddMinutes(5));

        await using var db = _db.CreateContext();
        db.Users.AddRange(customer, adminA, adminB);
        db.TransportCompanies.AddRange(companyA, companyB);
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        return (customer.Id, companyA, companyB, shipment);
    }

    [SqlServerFact]
    public async Task Shipment_AggregateRoundTrips_WithValueObjectsEventsAndUtcKinds()
    {
        var (_, companyA, _, original) = await SeedAcceptedShipment();

        await using var db = _db.CreateContext();
        var loaded = await db.Shipments.Include(s => s.Stops).Include(s => s.Quotes).Include(s => s.Events)
            .SingleAsync(s => s.Id == original.Id);

        Assert.Equal("REF-1", loaded.Reference);
        Assert.Equal(ShipmentStatus.Accepted, loaded.Status);
        Assert.Equal(original.Cargo, loaded.Cargo);
        Assert.Equal(Money.Of(1500.1234m, "USD"), loaded.AgreedPrice);
        Assert.Equal(companyA.Id, loaded.CarrierCompanyId);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);

        var stops = loaded.Stops.OrderBy(s => s.Sequence).ToList();
        Assert.Equal(new[] { StopType.Pickup, StopType.Waypoint, StopType.Delivery }, stops.Select(s => s.Type).ToArray());
        Assert.Equal(original.PickupStop.Window, stops[0].Window);
        Assert.Equal(DateTimeKind.Utc, stops[0].Window!.StartUtc.Kind);
        Assert.Null(stops[1].Window);
        Assert.Equal(original.DeliveryStop.Location, stops[2].Location);

        Assert.Equal(new[] { QuoteStatus.Accepted, QuoteStatus.Rejected }, loaded.Quotes.Select(q => q.Status).OrderBy(s => s).ToArray());
        Assert.All(loaded.Quotes, q => Assert.Equal(DateTimeKind.Utc, q.ExpiresAtUtc.Kind));

        Assert.Equal(original.Events.Count, loaded.Events.Count);
        Assert.Null(loaded.Events.OrderBy(e => e.OccurredAtUtc).First().PreviousStatus);
        var accepted = loaded.Events.Single(e => e.NewStatus == ShipmentStatus.Accepted);
        Assert.Equal(original.AcceptedQuoteId.ToString(), accepted.Metadata["quoteId"]);
        Assert.NotEmpty(db.Entry(loaded).Property<byte[]>("RowVersion").CurrentValue!);
    }

    [SqlServerFact]
    public async Task ChildAddedToATrackedAggregate_IsInserted_NotTreatedAsExisting()
    {
        var customer = NewUser("customer");
        var (company, admin) = NewCompany("Carrier");
        var shipment = NewShipment(customer.Id);
        shipment.OpenForQuoting(customer.Id, Now);
        await using (var seed = _db.CreateContext())
        {
            seed.Users.AddRange(customer, admin);
            seed.TransportCompanies.Add(company);
            seed.Shipments.Add(shipment);
            await seed.SaveChangesAsync();
        }

        await using (var db = _db.CreateContext())
        {
            var loaded = await db.Shipments.Include(s => s.Quotes).SingleAsync(s => s.Id == shipment.Id);
            loaded.SubmitQuote(company.Id, Money.Of(10m, "USD"), Now.AddHours(1), "t", Now);
            await db.SaveChangesAsync();
        }

        await using var verify = _db.CreateContext();
        Assert.Equal(1, await verify.Quotes.CountAsync(q => q.ShipmentId == shipment.Id));
    }

    [SqlServerFact]
    public async Task ConcurrentUpdatesToOneShipment_SecondWriterIsRejected()
    {
        var customer = NewUser("customer");
        var shipment = NewShipment(customer.Id);
        await using (var seed = _db.CreateContext())
        {
            seed.Users.Add(customer);
            seed.Shipments.Add(shipment);
            await seed.SaveChangesAsync();
        }

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var a = await first.Shipments.SingleAsync(s => s.Id == shipment.Id);
        var b = await second.Shipments.SingleAsync(s => s.Id == shipment.Id);

        a.OpenForQuoting(customer.Id, Now.AddMinutes(1));
        await first.SaveChangesAsync();
        b.Cancel(CancellationInitiator.Customer, customer.Id, "changed my mind", Now.AddMinutes(1));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Shipment_WithUnknownCustomer_IsRejectedByTheForeignKey()
    {
        await using var db = _db.CreateContext();
        db.Shipments.Add(NewShipment(Guid.NewGuid()));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task DatabaseRejectsASecondAcceptedQuote_EvenIfApplicationCodeIsBypassed()
    {
        var (_, _, _, shipment) = await SeedAcceptedShipment();
        var rejected = shipment.Quotes.Single(q => q.Status == QuoteStatus.Rejected);

        await using var db = _db.CreateContext();
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Quotes SET Status = 'Accepted' WHERE Id = {rejected.Id}"));
    }

    [SqlServerFact]
    public async Task DatabaseRejectsTwoLiveQuotesFromOneCompany()
    {
        var customer = NewUser("customer");
        var (company, admin) = NewCompany("Carrier");
        var shipment = NewShipment(customer.Id);
        shipment.OpenForQuoting(customer.Id, Now);
        var first = shipment.SubmitQuote(company.Id, Money.Of(10m, "USD"), Now.AddHours(1), "t", Now);
        shipment.WithdrawQuote(company.Id, first.Id, Now);
        shipment.SubmitQuote(company.Id, Money.Of(9m, "USD"), Now.AddHours(1), "t", Now);
        await using (var seed = _db.CreateContext())
        {
            seed.Users.AddRange(customer, admin);
            seed.TransportCompanies.Add(company);
            seed.Shipments.Add(shipment);
            await seed.SaveChangesAsync();
        }

        await using var db = _db.CreateContext();
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Quotes SET Status = 'Submitted' WHERE Id = {first.Id}"));
    }

    [SqlServerFact]
    public async Task VehiclePlates_AreUniquePerCompany_ButNotAcrossCompanies()
    {
        var (a, adminA) = NewCompany("A");
        var (b, adminB) = NewCompany("B");
        var capacity = Capacity.Of(1000m, 10m);
        await using (var db = _db.CreateContext())
        {
            db.Users.AddRange(adminA, adminB);
            db.TransportCompanies.AddRange(a, b);
            db.Vehicles.Add(a.RegisterVehicle("AB 123", VehicleType.Van, capacity));
            db.Vehicles.Add(b.RegisterVehicle("ab123", VehicleType.Van, capacity));
            await db.SaveChangesAsync();
        }

        await using var dup = _db.CreateContext();
        dup.Vehicles.Add(a.RegisterVehicle("ab 123", VehicleType.Truck, capacity));
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task MembershipHistory_AllowsRejoining_ButNotTwoActiveRows()
    {
        var (company, admin) = NewCompany("A");
        var member = NewUser("dispatcher");
        company.AddMember(member.Id, CompanyRole.Dispatcher, Now);
        await using (var db = _db.CreateContext())
        {
            db.Users.AddRange(admin, member);
            db.TransportCompanies.Add(company);
            await db.SaveChangesAsync();
        }

        await using (var db = _db.CreateContext())
        {
            var loaded = await db.TransportCompanies.Include(c => c.Memberships).SingleAsync(c => c.Id == company.Id);
            loaded.RemoveMember(member.Id, Now.AddDays(1));
            loaded.AddMember(member.Id, CompanyRole.Dispatcher, Now.AddDays(2));
            await db.SaveChangesAsync();
        }

        await using var verify = _db.CreateContext();
        Assert.Equal(2, await verify.CompanyMemberships.CountAsync(m => m.UserId == member.Id));
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() =>
            verify.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CompanyMemberships SET IsActive = 1 WHERE UserId = {member.Id} AND IsActive = 0"));
    }

    [SqlServerFact]
    public async Task FullLifecycle_FleetAssignmentTrackingAndProof_RoundTrip()
    {
        var customer = NewUser("customer");
        var (company, admin) = NewCompany("Carrier");
        var driverUser = NewUser("driver");
        var dispatcher = NewUser("dispatcher");
        company.AddMember(driverUser.Id, CompanyRole.Driver, Now);
        var driver = company.RegisterDriver(driverUser.Id, "Dan Driver", "lic-77");
        var vehicle = company.RegisterVehicle("xy 999", VehicleType.Truck, Capacity.Of(5000m, 40m));

        var shipment = NewShipment(customer.Id);
        shipment.OpenForQuoting(customer.Id, Now);
        var quote = shipment.SubmitQuote(company.Id, Money.Of(900m, "EGP"), Now.AddHours(1), "t", Now);
        shipment.AcceptQuote(customer.Id, quote.Id, Now);
        var window = TimeWindow.Of(Now.AddDays(1), Now.AddDays(1).AddHours(6));
        var assignment = Assignment.Create(shipment, driver, vehicle, window, dispatcher.Id, [], Now);
        shipment.PickUp(driverUser.Id, Now.AddDays(1));
        shipment.StartTransit(driverUser.Id, Now.AddDays(1));
        var point = TrackingPoint.Create(shipment, driverUser.Id, GeoPoint.Of(30.1, 31.2), Now.AddDays(1), Now.AddDays(1), 62.5m);
        var proof = DeliveryProof.Create(shipment, driverUser.Id, "R. Ecipient", Now.AddDays(1), Now.AddDays(1), GeoPoint.Of(31.2, 29.9), "left at desk");
        shipment.Deliver(driverUser.Id, Now.AddDays(1));
        assignment.Complete(shipment, Now.AddDays(1));

        await using (var db = _db.CreateContext())
        {
            db.Users.AddRange(customer, admin, driverUser, dispatcher);
            db.TransportCompanies.Add(company);
            db.Drivers.Add(driver);
            db.Vehicles.Add(vehicle);
            db.Shipments.Add(shipment);
            db.Assignments.Add(assignment);
            db.TrackingPoints.Add(point);
            db.DeliveryProofs.Add(proof);
            await db.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loadedVehicle = await read.Vehicles.SingleAsync(v => v.Id == vehicle.Id);
        Assert.Equal("XY999", loadedVehicle.PlateNumber);
        Assert.Equal(vehicle.Capacity, loadedVehicle.Capacity);
        Assert.Equal("LIC-77", (await read.Drivers.SingleAsync(d => d.Id == driver.Id)).LicenseNumber);

        var loadedAssignment = await read.Assignments.SingleAsync(a => a.Id == assignment.Id);
        Assert.Equal(AssignmentStatus.Completed, loadedAssignment.Status);
        Assert.Equal(window, loadedAssignment.Window);
        Assert.Equal(DateTimeKind.Utc, loadedAssignment.Window.EndUtc.Kind);

        var loadedPoint = await read.TrackingPoints.SingleAsync(t => t.Id == point.Id);
        Assert.Equal(62.5m, loadedPoint.SpeedKph);
        Assert.Equal(point.Location, loadedPoint.Location);

        var loadedProof = await read.DeliveryProofs.SingleAsync(p => p.Id == proof.Id);
        Assert.Equal(proof.Location, loadedProof.Location);
        Assert.Equal("left at desk", loadedProof.Notes);

        var finalShipment = await read.Shipments.Include(s => s.Events).SingleAsync(s => s.Id == shipment.Id);
        Assert.Equal(ShipmentStatus.Delivered, finalShipment.Status);
        Assert.Equal(shipment.Events.Count, finalShipment.Events.Count);
    }
}
