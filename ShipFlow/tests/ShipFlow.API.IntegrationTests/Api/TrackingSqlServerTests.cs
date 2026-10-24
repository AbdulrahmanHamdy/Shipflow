using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ShipFlow.API.IntegrationTests.Support;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Payments;
using ShipFlow.Application.Features.Tracking;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;
using ShipFlow.Domain.ValueObjects;
using static ShipFlow.API.IntegrationTests.Support.PersistenceData;

namespace ShipFlow.API.IntegrationTests.Api;

public class TrackingSqlServerTests : IClassFixture<SqlServerDatabaseFixture>, IDisposable
{
    private readonly SqlServerDatabaseFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public TrackingSqlServerTests(SqlServerDatabaseFixture db)
    {
        _db = db;
        _factory = new ShipFlowApiFactory().WithSettings(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = SqlServerTestSettings.IsConfigured ? db.ConnectionString : "Server=localhost;Database=Unused"
        });
    }

    public void Dispose() => _factory.Dispose();

    private sealed record Actor(Guid Id, string Role, Guid? CompanyId = null)
    {
        public string Token => TestTokens.Create([Role], userId: Id);
    }

    private HttpClient As(Actor actor)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", actor.Token);
        return client;
    }

    private async Task<(Shipment Shipment, Assignment Assignment, Actor Customer, Actor Driver, TransportCompany Company)> SeedAssignedShipmentAsync()
    {
        var customer = NewUser("customer");
        var driverUser = NewUser("driver");
        var (company, admin) = NewCompany("Speedy Freight");

        company.AddMember(driverUser.Id, CompanyRole.Driver, Now);
        var driver = company.RegisterDriver(driverUser.Id, driverUser.FullName, "LIC-" + Guid.NewGuid().ToString("N")[..6]);
        var vehicle = company.RegisterVehicle("PL-" + Guid.NewGuid().ToString("N")[..4], VehicleType.Van, Capacity.Of(5000m, 30m));

        var shipment = NewShipment(customer.Id);
        shipment.OpenForQuoting(customer.Id, Now.AddSeconds(1));
        var quote = shipment.SubmitQuote(company.Id, Money.Of(500m, "USD"), Now.AddHours(2), "Net 30", Now);
        shipment.AcceptQuote(customer.Id, quote.Id, Now.AddMinutes(5));

        var window = TimeWindow.Of(Now.AddDays(1), Now.AddDays(1).AddHours(2));
        var assignment = Assignment.Create(shipment, driver, vehicle, window, admin.Id, [], Now);

        await using var db = _db.CreateContext();
        db.Users.AddRange(customer, driverUser, admin);
        db.TransportCompanies.Add(company);
        db.Drivers.Add(driver);
        db.Vehicles.Add(vehicle);
        db.Shipments.Add(shipment);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        return (shipment, assignment, new Actor(customer.Id, Roles.Customer), new Actor(driverUser.Id, Roles.Driver, company.Id), company);
    }

    [SqlServerFact]
    public async Task DriverLifecycle_Pickup_Transit_Deliver_CompletesSuccessfully()
    {
        var (shipment, assignment, customer, driver, _) = await SeedAssignedShipmentAsync();

        var driverClient = As(driver);
        var customerClient = As(customer);

        // 1. Pickup
        var pickupRes = await driverClient.PostAsync($"/api/shipments/{shipment.Id}/pickup", null);
        Assert.Equal(HttpStatusCode.OK, pickupRes.StatusCode);

        // 2. Start transit
        var transitRes = await driverClient.PostAsync($"/api/shipments/{shipment.Id}/start-transit", null);
        Assert.Equal(HttpStatusCode.OK, transitRes.StatusCode);

        // 3. Submit tracking point while in transit
        var trackBody = new SubmitTrackingPointCommand(shipment.Id, 30.0444, 31.2357, DateTime.UtcNow, 45.5m);
        var trackRes = await driverClient.PostAsJsonAsync("/api/tracking", trackBody);
        Assert.Equal(HttpStatusCode.OK, trackRes.StatusCode);

        // 4. Read latest position as customer
        var latestRes = await customerClient.GetAsync($"/api/shipments/{shipment.Id}/tracking/latest");
        Assert.Equal(HttpStatusCode.OK, latestRes.StatusCode);

        // 5. Deliver with delivery proof
        var deliverBody = new DeliverShipmentCommand(DateTime.UtcNow, "Sarah Connor", "Left with concierge", null, null);
        var deliverRes = await driverClient.PostAsJsonAsync($"/api/shipments/{shipment.Id}/deliver", deliverBody);
        Assert.Equal(HttpStatusCode.OK, deliverRes.StatusCode);

        // 6. Verify in DB: shipment is Delivered and assignment is Completed
        await using var db = _db.CreateContext();
        var deliveredShipment = await db.Shipments.SingleAsync(s => s.Id == shipment.Id);
        Assert.Equal(ShipmentStatus.Delivered, deliveredShipment.Status);

        var completedAssignment = await db.Assignments.SingleAsync(a => a.Id == assignment.Id);
        Assert.Equal(AssignmentStatus.Completed, completedAssignment.Status);

        // 7. Verify delivery proof can be fetched
        var proofRes = await customerClient.GetAsync($"/api/shipments/{shipment.Id}/delivery-proof");
        Assert.Equal(HttpStatusCode.OK, proofRes.StatusCode);
    }

    [SqlServerFact]
    public async Task DriverLifecycle_FailAndRetryDelivery()
    {
        var (shipment, _, _, driver, _) = await SeedAssignedShipmentAsync();
        var driverClient = As(driver);

        await driverClient.PostAsync($"/api/shipments/{shipment.Id}/pickup", null);
        await driverClient.PostAsync($"/api/shipments/{shipment.Id}/start-transit", null);

        var failBody = new FailDeliveryCommand("Customer not answering door");
        var failRes = await driverClient.PostAsJsonAsync($"/api/shipments/{shipment.Id}/fail-delivery", failBody);
        Assert.Equal(HttpStatusCode.OK, failRes.StatusCode);

        await using (var db = _db.CreateContext())
        {
            var loaded = await db.Shipments.SingleAsync(s => s.Id == shipment.Id);
            Assert.Equal(ShipmentStatus.DeliveryFailed, loaded.Status);
        }

        var retryRes = await driverClient.PostAsync($"/api/shipments/{shipment.Id}/retry-delivery", null);
        Assert.Equal(HttpStatusCode.OK, retryRes.StatusCode);

        await using (var db = _db.CreateContext())
        {
            var loaded = await db.Shipments.SingleAsync(s => s.Id == shipment.Id);
            Assert.Equal(ShipmentStatus.InTransit, loaded.Status);
        }
    }

    [SqlServerFact]
    public async Task GetCurrentDriverAssignment_ReturnsActiveAssignment()
    {
        var (shipment, assignment, _, driver, _) = await SeedAssignedShipmentAsync();
        var driverClient = As(driver);

        var res = await driverClient.GetAsync("/api/driver/assignments/current");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var current = await res.Content.ReadFromJsonAsync<AssignmentDto>();
        Assert.NotNull(current);
        Assert.Equal(assignment.Id, current.Id);
        Assert.Equal(shipment.Id, current.ShipmentId);
    }

    [SqlServerFact]
    public async Task Concurrency_TwoSimultaneousDeliveries_OnlyOneWins()
    {
        var (shipment, _, _, driver, _) = await SeedAssignedShipmentAsync();
        var driverClient1 = As(driver);
        var driverClient2 = As(driver);

        await driverClient1.PostAsync($"/api/shipments/{shipment.Id}/pickup", null);
        await driverClient1.PostAsync($"/api/shipments/{shipment.Id}/start-transit", null);

        var deliverBody1 = new DeliverShipmentCommand(DateTime.UtcNow, "Recipient 1", null, null, null);
        var deliverBody2 = new DeliverShipmentCommand(DateTime.UtcNow, "Recipient 2", null, null, null);

        using var gate = new ManualResetEventSlim(false);
        var t1 = Task.Run(async () =>
        {
            gate.Wait();
            return await driverClient1.PostAsJsonAsync($"/api/shipments/{shipment.Id}/deliver", deliverBody1);
        });
        var t2 = Task.Run(async () =>
        {
            gate.Wait();
            return await driverClient2.PostAsJsonAsync($"/api/shipments/{shipment.Id}/deliver", deliverBody2);
        });
        gate.Set();

        var responses = await Task.WhenAll(t1, t2);
        var statuses = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();

        var body0 = await responses[0].Content.ReadAsStringAsync();
        var body1 = await responses[1].Content.ReadAsStringAsync();

        Assert.True(
            statuses.Contains(HttpStatusCode.OK) &&
            (statuses.Contains(HttpStatusCode.Conflict) || statuses.Contains(HttpStatusCode.BadRequest) || statuses.Contains(HttpStatusCode.UnprocessableEntity)),
            $"Actual statuses: [{string.Join(", ", statuses)}]. Response0: {(int)responses[0].StatusCode} {body0}. Response1: {(int)responses[1].StatusCode} {body1}");
    }
}
