using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ShipFlow.API.IntegrationTests.Support;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.ValueObjects;
using static ShipFlow.API.IntegrationTests.Support.PersistenceData;

namespace ShipFlow.API.IntegrationTests.Api;

/// <summary>
/// Phase 4 against a real SQL Server: onboarding and role sync (real register/login), fleet, tenant isolation, role boundaries,
/// assignment eligibility, window rules, cancel-releases, member removal, and the concurrent double-booking race.
/// Skipped (never "passed") without SHIPFLOW_TEST_SQLSERVER. NOT RUN in the authoring session (no SQL Server available).
/// </summary>
public class FleetAndDispatchingSqlServerTests : IClassFixture<SqlServerDatabaseFixture>, IDisposable
{
    private const string Password = "Str0ngPassw0rd";

    private readonly SqlServerDatabaseFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public FleetAndDispatchingSqlServerTests(SqlServerDatabaseFixture db)
    {
        _db = db;
        _factory = new ShipFlowApiFactory().WithSettings(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = SqlServerTestSettings.IsConfigured ? db.ConnectionString : "Server=localhost;Database=Unused",
            // Identity roles must exist for the membership/role sync endpoints.
            ["Database:SeedReferenceData"] = "true"
        });
    }

    public void Dispose() => _factory.Dispose();

    private sealed record Actor(Guid Id, string Role)
    {
        public string Token => TestTokens.Create([Role], userId: Id);
    }

    private sealed record World(
        Actor Customer, Actor AdminA, Actor DispatcherA, Actor DriverA, Actor AdminB, Actor DispatcherB,
        Guid CompanyA, Guid CompanyB, Guid DriverProfileA, Guid TruckA);

    private async Task<World> CreateWorld(decimal truckKg = 5000m, decimal truckM3 = 50m)
    {
        var customer = NewUser("customer");
        var dispatcherA = NewUser("dispatcherA");
        var driverUser = NewUser("driverA");
        var dispatcherB = NewUser("dispatcherB");
        var (companyA, adminA) = NewCompany("Carrier A");
        var (companyB, adminB) = NewCompany("Carrier B");
        companyA.AddMember(dispatcherA.Id, CompanyRole.Dispatcher, Now);
        companyA.AddMember(driverUser.Id, CompanyRole.Driver, Now);
        companyB.AddMember(dispatcherB.Id, CompanyRole.Dispatcher, Now);
        var driver = companyA.RegisterDriver(driverUser.Id, "Dan Driver", "LIC-A1");
        var truck = companyA.RegisterVehicle("TRK 1", VehicleType.Truck, Capacity.Of(truckKg, truckM3));

        await using var db = _db.CreateContext();
        db.Users.AddRange(customer, dispatcherA, driverUser, dispatcherB, adminA, adminB);
        db.TransportCompanies.AddRange(companyA, companyB);
        db.Drivers.Add(driver);
        db.Vehicles.Add(truck);
        await db.SaveChangesAsync();

        return new World(
            new Actor(customer.Id, Roles.Customer), new Actor(adminA.Id, Roles.CompanyAdmin),
            new Actor(dispatcherA.Id, Roles.Dispatcher), new Actor(driverUser.Id, Roles.Driver),
            new Actor(adminB.Id, Roles.CompanyAdmin), new Actor(dispatcherB.Id, Roles.Dispatcher),
            companyA.Id, companyB.Id, driver.Id, truck.Id);
    }

    private HttpClient As(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient As(Actor actor) => As(actor.Token);

    private static async Task<Guid> Id(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    private static async Task<string?> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    private static object Body(double kg = 500, double m3 = 5) => new
    {
        reference = "R-" + Guid.NewGuid().ToString("N")[..8],
        cargo = new { description = "Pallets", weightKg = kg, volumeM3 = m3 },
        stops = new object[]
        {
            new { address = "1 Pickup Street", latitude = 30.04, longitude = 31.23, windowStartUtc = "2031-01-02T08:00:00Z", windowEndUtc = "2031-01-02T10:00:00Z" },
            new { address = "9 Delivery Avenue", latitude = 31.2, longitude = 29.9 }
        }
    };

    /// <summary>Customer creates, opens for quotes; dispatcher A quotes; customer accepts: an Accepted shipment of company A.</summary>
    private async Task<Guid> AcceptedShipment(World w, double kg = 500, double m3 = 5)
    {
        var created = await As(w.Customer).PostAsJsonAsync("/api/shipments", Body(kg, m3));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = await Id(created);
        Assert.Equal(HttpStatusCode.OK, (await As(w.Customer).PostAsync($"/api/shipments/{id}/open-for-quotes", null)).StatusCode);
        var quote = await As(w.DispatcherA).PostAsJsonAsync($"/api/shipments/{id}/quotes",
            new { amount = 100m, currency = "USD", expiresAtUtc = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ssZ"), terms = "Net 30" });
        Assert.Equal(HttpStatusCode.Created, quote.StatusCode);
        var accepted = await As(w.Customer).PostAsync($"/api/shipments/{id}/quotes/{await Id(quote)}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return id;
    }

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static (DateTime Start, DateTime End) Slot(int startHours, int hours = 4)
    {
        var start = DateTime.UtcNow.Date.AddDays(10).AddHours(startHours);
        return (start, start.AddHours(hours));
    }

    private static object AssignBody(Guid driver, Guid vehicle, (DateTime Start, DateTime End) slot) =>
        new { driverId = driver, vehicleId = vehicle, windowStartUtc = Iso(slot.Start), windowEndUtc = Iso(slot.End) };

    private Task<HttpResponseMessage> Assign(Actor who, Guid shipment, Guid driver, Guid vehicle, (DateTime Start, DateTime End) slot) =>
        As(who).PostAsJsonAsync($"/api/shipments/{shipment}/assignment", AssignBody(driver, vehicle, slot));

    private async Task<Guid> AddVehicle(Actor admin, string plate, decimal kg = 5000m, decimal m3 = 50m)
    {
        var response = await As(admin).PostAsJsonAsync("/api/fleet/vehicles", new { plateNumber = plate, type = "Truck", maxWeightKg = kg, maxVolumeM3 = m3 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Id(response);
    }

    private async Task<Guid> AddDriver(World w, string license)
    {
        var user = NewUser("extra-driver");
        await using (var db = _db.CreateContext())
        {
            var company = await db.TransportCompanies.Include(c => c.Memberships).SingleAsync(c => c.Id == w.CompanyA);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            company.AddMember(user.Id, CompanyRole.Driver, Now);
            await db.SaveChangesAsync();
        }

        var response = await As(w.AdminA).PostAsJsonAsync("/api/fleet/drivers", new { userId = user.Id, fullName = "Extra " + license, licenseNumber = license });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Id(response);
    }

    // ------------------------------------------------------------------ tenant isolation and roles

    [SqlServerFact]
    public async Task OtherCompaniesResources_AreNotFound()
    {
        var w = await CreateWorld();
        var shipment = await AcceptedShipment(w);
        var otherVehicle = await AddVehicle(w.AdminB, "OTH 1");
        var slot = Slot(8);

        Assert.Equal(HttpStatusCode.NotFound, (await As(w.DispatcherB).GetAsync($"/api/fleet/drivers/{w.DriverProfileA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.AdminB).GetAsync($"/api/fleet/vehicles/{w.TruckA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.AdminB).PostAsync($"/api/fleet/vehicles/{w.TruckA}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.AdminB).PostAsync($"/api/fleet/drivers/{w.DriverProfileA}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Assign(w.DispatcherB, shipment, w.DriverProfileA, otherVehicle, slot)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.DispatcherB).GetAsync($"/api/shipments/{shipment}/assignment")).StatusCode);
        var query = $"?windowStartUtc={Iso(slot.Start)}&windowEndUtc={Iso(slot.End)}";
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.DispatcherB).GetAsync($"/api/shipments/{shipment}/available-vehicles{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.DispatcherB).GetAsync($"/api/shipments/{shipment}/available-drivers{query}")).StatusCode);

        // Company A dispatching with company B's vehicle: not found, not forbidden.
        Assert.Equal(HttpStatusCode.NotFound, (await Assign(w.DispatcherA, shipment, w.DriverProfileA, otherVehicle, slot)).StatusCode);

        var listB = await As(w.AdminB).GetFromJsonAsync<JsonElement>("/api/fleet/vehicles");
        Assert.Equal(1, listB.GetProperty("totalCount").GetInt32());
    }

    [SqlServerFact]
    public async Task RoleBoundaries_AreEnforced()
    {
        var w = await CreateWorld();
        var shipment = await AcceptedShipment(w);
        var slot = Slot(8);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/fleet/drivers")).StatusCode);
        // Dispatcher: may read the fleet and dispatch, may not change the fleet or members.
        Assert.Equal(HttpStatusCode.OK, (await As(w.DispatcherA).GetAsync("/api/fleet/drivers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.DispatcherA).PostAsJsonAsync("/api/fleet/vehicles", new { plateNumber = "X 1", type = "Van", maxWeightKg = 1m, maxVolumeM3 = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.DispatcherA).GetAsync("/api/companies/me/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.DispatcherA).PostAsync($"/api/fleet/drivers/{w.DriverProfileA}/deactivate", null)).StatusCode);
        // Driver and customer cannot dispatch or read the fleet.
        Assert.Equal(HttpStatusCode.Forbidden, (await Assign(w.DriverA, shipment, w.DriverProfileA, w.TruckA, slot)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Assign(w.Customer, shipment, w.DriverProfileA, w.TruckA, slot)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.DriverA).GetAsync("/api/fleet/vehicles")).StatusCode);
        // Admin: may do both.
        Assert.Equal(HttpStatusCode.Created, (await Assign(w.AdminA, shipment, w.DriverProfileA, w.TruckA, slot)).StatusCode);
    }

    [SqlServerFact]
    public async Task AToken_WithAStaleRole_StillCannotActAfterTheMembershipEnded()
    {
        var w = await CreateWorld();
        var shipment = await AcceptedShipment(w);
        Assert.Equal(HttpStatusCode.NoContent, (await As(w.AdminA).DeleteAsync($"/api/companies/me/members/{w.DispatcherA.Id}")).StatusCode);

        // The old token still says Dispatcher, but membership is read from the database on every call.
        var response = await Assign(w.DispatcherA, shipment, w.DriverProfileA, w.TruckA, Slot(8));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------ assignment rules

    [SqlServerFact]
    public async Task Assign_Eligibility_Capacity_Inactive_AndStatus()
    {
        var w = await CreateWorld();
        var heavy = await AcceptedShipment(w, kg: 9000, m3: 5);
        var normal = await AcceptedShipment(w);
        var slot = Slot(8);

        var tooBig = await Assign(w.DispatcherA, heavy, w.DriverProfileA, w.TruckA, slot);
        Assert.Equal((HttpStatusCode)422, tooBig.StatusCode);
        Assert.Equal("capacity_exceeded", await Code(tooBig));

        var spare = await AddVehicle(w.AdminA, "SPARE 1");
        Assert.Equal(HttpStatusCode.OK, (await As(w.AdminA).PostAsync($"/api/fleet/vehicles/{spare}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Assign(w.DispatcherA, normal, w.DriverProfileA, spare, slot)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await As(w.AdminA).PostAsync($"/api/fleet/drivers/{w.DriverProfileA}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Assign(w.DispatcherA, normal, w.DriverProfileA, w.TruckA, slot)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await As(w.AdminA).PostAsync($"/api/fleet/drivers/{w.DriverProfileA}/activate", null)).StatusCode);

        var created = await As(w.Customer).PostAsJsonAsync("/api/shipments", Body());
        var pendingId = await Id(created);
        Assert.Equal(HttpStatusCode.NotFound, (await Assign(w.DispatcherA, pendingId, w.DriverProfileA, w.TruckA, slot)).StatusCode);

        var ok = await Assign(w.DispatcherA, normal, w.DriverProfileA, w.TruckA, slot);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var shipment = await As(w.Customer).GetFromJsonAsync<JsonElement>($"/api/shipments/{normal}");
        Assert.Equal("Assigned", shipment.GetProperty("status").GetString());
        // A shipment can only be assigned once; the driver cannot be deactivated while booked.
        Assert.Equal(HttpStatusCode.Conflict, (await Assign(w.DispatcherA, normal, w.DriverProfileA, w.TruckA, Slot(20))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await As(w.AdminA).PostAsync($"/api/fleet/drivers/{w.DriverProfileA}/deactivate", null)).StatusCode);
    }

    [SqlServerFact]
    public async Task Windows_OverlapConflicts_BackToBackIsAllowed_AndAvailabilityAgrees()
    {
        var w = await CreateWorld();
        var other = await AddDriver(w, "LIC-A2");
        var truck2 = await AddVehicle(w.AdminA, "TRK 2");
        var s1 = await AcceptedShipment(w);
        var s2 = await AcceptedShipment(w);
        var s3 = await AcceptedShipment(w);
        Assert.Equal(HttpStatusCode.Created, (await Assign(w.DispatcherA, s1, w.DriverProfileA, w.TruckA, Slot(8, 4))).StatusCode);

        var sameDriver = await Assign(w.DispatcherA, s2, w.DriverProfileA, truck2, Slot(10, 4));
        Assert.Equal(HttpStatusCode.Conflict, sameDriver.StatusCode);
        Assert.Equal("assignment_conflict", await Code(sameDriver));
        var sameVehicle = await Assign(w.DispatcherA, s2, other, w.TruckA, Slot(11, 4));
        Assert.Equal(HttpStatusCode.Conflict, sameVehicle.StatusCode);

        var (start, end) = Slot(10, 2);
        var vehicles = await As(w.DispatcherA).GetFromJsonAsync<JsonElement[]>(
            $"/api/shipments/{s2}/available-vehicles?windowStartUtc={Iso(start)}&windowEndUtc={Iso(end)}");
        Assert.DoesNotContain(vehicles!, v => v.GetProperty("id").GetGuid() == w.TruckA);
        var drivers = await As(w.DispatcherA).GetFromJsonAsync<JsonElement[]>(
            $"/api/shipments/{s2}/available-drivers?windowStartUtc={Iso(start)}&windowEndUtc={Iso(end)}");
        Assert.DoesNotContain(drivers!, d => d.GetProperty("id").GetGuid() == w.DriverProfileA);

        // Starting exactly when the first window ends is fine: windows are half-open.
        Assert.Equal(HttpStatusCode.Created, (await Assign(w.DispatcherA, s3, w.DriverProfileA, w.TruckA, Slot(12, 4))).StatusCode);
        var (bStart, bEnd) = Slot(12, 2);
        var free = await As(w.DispatcherA).GetFromJsonAsync<JsonElement[]>(
            $"/api/shipments/{s2}/available-drivers?windowStartUtc={Iso(bStart)}&windowEndUtc={Iso(bEnd)}");
        Assert.Contains(free!, d => d.GetProperty("id").GetGuid() == other);
    }

    [SqlServerFact]
    public async Task AvailableVehicles_RespectCapacity()
    {
        var w = await CreateWorld();
        var small = await AddVehicle(w.AdminA, "SMALL 1", 100m, 1m);
        var shipment = await AcceptedShipment(w, kg: 500, m3: 5);
        var (start, end) = Slot(8);

        var vehicles = await As(w.DispatcherA).GetFromJsonAsync<JsonElement[]>(
            $"/api/shipments/{shipment}/available-vehicles?windowStartUtc={Iso(start)}&windowEndUtc={Iso(end)}");

        Assert.Contains(vehicles!, v => v.GetProperty("id").GetGuid() == w.TruckA);
        Assert.DoesNotContain(vehicles!, v => v.GetProperty("id").GetGuid() == small);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await As(w.DispatcherA).GetAsync($"/api/shipments/{shipment}/available-vehicles?windowStartUtc={Iso(end)}&windowEndUtc={Iso(start)}")).StatusCode);
    }

    [SqlServerFact]
    public async Task Unassign_ReturnsToAccepted_AndAllowsReassignment()
    {
        var w = await CreateWorld();
        var shipment = await AcceptedShipment(w);
        Assert.Equal(HttpStatusCode.Created, (await Assign(w.DispatcherA, shipment, w.DriverProfileA, w.TruckA, Slot(8))).StatusCode);
        var current = await As(w.DispatcherA).GetFromJsonAsync<JsonElement>($"/api/shipments/{shipment}/assignment");
        Assert.Equal("Active", current.GetProperty("status").GetString());

        var unassigned = await As(w.DispatcherA).PostAsJsonAsync($"/api/shipments/{shipment}/unassign", new { reason = "wrong truck" });
        Assert.Equal(HttpStatusCode.OK, unassigned.StatusCode);
        Assert.Equal("Accepted", (await unassigned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.DispatcherA).GetAsync($"/api/shipments/{shipment}/assignment")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Assign(w.DispatcherA, shipment, w.DriverProfileA, w.TruckA, Slot(8))).StatusCode);

        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.ShipmentId == shipment && a.Status == AssignmentStatus.Active));
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.ShipmentId == shipment && a.Status == AssignmentStatus.Released));
    }

    [SqlServerFact]
    public async Task Cancel_ReleasesTheBooking_SoTheResourcesAreFree()
    {
        var w = await CreateWorld();
        var first = await AcceptedShipment(w);
        var second = await AcceptedShipment(w);
        var slot = Slot(8);
        Assert.Equal(HttpStatusCode.Created, (await Assign(w.DispatcherA, first, w.DriverProfileA, w.TruckA, slot)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Assign(w.DispatcherA, second, w.DriverProfileA, w.TruckA, slot)).StatusCode);

        var cancelled = await As(w.Customer).PostAsJsonAsync($"/api/shipments/{first}/cancel", new { reason = "no longer needed" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await Assign(w.DispatcherA, second, w.DriverProfileA, w.TruckA, slot)).StatusCode);
        await using var db = _db.CreateContext();
        Assert.Equal(AssignmentStatus.Released, (await db.Assignments.SingleAsync(a => a.ShipmentId == first)).Status);
    }

    // ------------------------------------------------------------------ fleet

    [SqlServerFact]
    public async Task Fleet_DuplicatePlateAndLicence_AreConflicts_AndDetailsCanBeUpdated()
    {
        var w = await CreateWorld();
        var dup = await As(w.AdminA).PostAsJsonAsync("/api/fleet/vehicles", new { plateNumber = "trk  1", type = "Van", maxWeightKg = 10m, maxVolumeM3 = 1m });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        // The same plate in another company is fine: uniqueness is per company.
        Assert.Equal(HttpStatusCode.Created, (await As(w.AdminB).PostAsJsonAsync("/api/fleet/vehicles", new { plateNumber = "TRK 1", type = "Van", maxWeightKg = 10m, maxVolumeM3 = 1m })).StatusCode);

        var second = await AddDriver(w, "LIC-A3");
        var clash = await As(w.AdminA).PutAsJsonAsync($"/api/fleet/drivers/{second}", new { fullName = "New Name", licenseNumber = "lic-a1" });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var ok = await As(w.AdminA).PutAsJsonAsync($"/api/fleet/drivers/{second}", new { fullName = "New Name", licenseNumber = "lic-a9" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("LIC-A9", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("licenseNumber").GetString());

        var capacity = await As(w.AdminA).PutAsJsonAsync($"/api/fleet/vehicles/{w.TruckA}/capacity", new { maxWeightKg = 7000.5m, maxVolumeM3 = 70m });
        Assert.Equal(HttpStatusCode.OK, capacity.StatusCode);
        var reread = await As(w.AdminA).GetFromJsonAsync<JsonElement>($"/api/fleet/vehicles/{w.TruckA}");
        Assert.Equal(7000.5m, reread.GetProperty("maxWeightKg").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await As(w.AdminA).PutAsJsonAsync($"/api/fleet/vehicles/{w.TruckA}/capacity", new { maxWeightKg = 0m, maxVolumeM3 = 70m })).StatusCode);
    }

    [SqlServerFact]
    public async Task RegisterDriver_RejectsAUserWhoIsNotADriverMemberOfTheCompany()
    {
        var w = await CreateWorld();
        var response = await As(w.AdminA).PostAsJsonAsync("/api/fleet/drivers", new { userId = w.DispatcherA.Id, fullName = "Not A Driver", licenseNumber = "LIC-X" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var foreign = await As(w.AdminA).PostAsJsonAsync("/api/fleet/drivers", new { userId = w.DispatcherB.Id, fullName = "Foreign", licenseNumber = "LIC-Y" });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    // ------------------------------------------------------------------ onboarding, membership and role sync (real auth)

    private async Task<(Guid Id, string Email)> RegisterUser(string label)
    {
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = label });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Id(response), email);
    }

    private async Task<string> Login(string email)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private async Task<string[]> RolesInDatabase(Guid userId)
    {
        await using var db = _db.CreateContext();
        var names = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == userId orderby r.Name select r.Name).ToListAsync();
        return names.Select(n => n ?? string.Empty).ToArray();
    }

    [SqlServerFact]
    public async Task Onboarding_MembershipsAndIdentityRoles_StayConsistent()
    {
        var (adminId, adminEmail) = await RegisterUser("founder");
        var oldToken = await Login(adminEmail);

        var created = await As(oldToken).PostAsJsonAsync("/api/companies", new { name = "Fresh Freight" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(new[] { Roles.CompanyAdmin, Roles.Customer }, await RolesInDatabase(adminId));
        // The old token carries only Customer, so it cannot use admin endpoints until the user signs in again.
        Assert.Equal(HttpStatusCode.Forbidden, (await As(oldToken).GetAsync("/api/companies/me/members")).StatusCode);
        var adminToken = await Login(adminEmail);
        Assert.Equal(HttpStatusCode.OK, (await As(adminToken).GetAsync("/api/companies/me")).StatusCode);
        // One company per user.
        Assert.Equal(HttpStatusCode.Conflict, (await As(oldToken).PostAsJsonAsync("/api/companies", new { name = "Second" })).StatusCode);

        var (driverId, driverEmail) = await RegisterUser("driver");
        var (dispatcherId, dispatcherEmail) = await RegisterUser("dispatcher");
        Assert.Equal(HttpStatusCode.Created, (await As(adminToken).PostAsJsonAsync("/api/companies/me/members", new { email = driverEmail, role = "Driver" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await As(adminToken).PostAsJsonAsync("/api/companies/me/members", new { email = dispatcherEmail, role = "Dispatcher" })).StatusCode);
        Assert.Equal(new[] { Roles.Customer, Roles.Driver }, await RolesInDatabase(driverId));
        Assert.Equal(new[] { Roles.Customer, Roles.Dispatcher }, await RolesInDatabase(dispatcherId));

        Assert.Equal(HttpStatusCode.Conflict, (await As(adminToken).PostAsJsonAsync("/api/companies/me/members", new { email = driverEmail, role = "Dispatcher" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await As(adminToken).PostAsJsonAsync("/api/companies/me/members", new { email = driverEmail, role = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(adminToken).PostAsJsonAsync("/api/companies/me/members", new { email = "nobody@example.com", role = "Driver" })).StatusCode);

        var members = await As(adminToken).GetFromJsonAsync<JsonElement[]>("/api/companies/me/members");
        Assert.Equal(3, members!.Length);

        // The new dispatcher signs in again and sees the fleet; the driver profile is registered by the admin.
        var dispatcherToken = await Login(dispatcherEmail);
        Assert.Equal(HttpStatusCode.OK, (await As(dispatcherToken).GetAsync("/api/fleet/vehicles")).StatusCode);
        var profile = await As(adminToken).PostAsJsonAsync("/api/fleet/drivers", new { userId = driverId, fullName = "Dina Driver", licenseNumber = "LIC-F1" });
        Assert.Equal(HttpStatusCode.Created, profile.StatusCode);
        var profileId = await Id(profile);

        // Removing the driver: membership closed, role revoked, driver profile deactivated, all together.
        Assert.Equal(HttpStatusCode.NoContent, (await As(adminToken).DeleteAsync($"/api/companies/me/members/{driverId}")).StatusCode);
        Assert.Equal(new[] { Roles.Customer }, await RolesInDatabase(driverId));
        var driver = await As(adminToken).GetFromJsonAsync<JsonElement>($"/api/fleet/drivers/{profileId}");
        Assert.False(driver.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await As(adminToken).PostAsync($"/api/fleet/drivers/{profileId}/activate", null)).StatusCode);
        await using (var db = _db.CreateContext())
        {
            Assert.False(await db.CompanyMemberships.AnyAsync(m => m.UserId == driverId && m.IsActive));
        }

        // The removed user can join a company again (history rows are kept).
        Assert.Equal(HttpStatusCode.Created, (await As(adminToken).PostAsJsonAsync("/api/companies/me/members", new { email = driverEmail, role = "Driver" })).StatusCode);
        // The last administrator cannot be removed.
        Assert.Equal(HttpStatusCode.Conflict, (await As(adminToken).DeleteAsync($"/api/companies/me/members/{adminId}")).StatusCode);
    }

    [SqlServerFact]
    public async Task RemovingADriverWithAnActiveBooking_IsRefused()
    {
        var w = await CreateWorld();
        var shipment = await AcceptedShipment(w);
        Assert.Equal(HttpStatusCode.Created, (await Assign(w.DispatcherA, shipment, w.DriverProfileA, w.TruckA, Slot(8))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await As(w.AdminA).DeleteAsync($"/api/companies/me/members/{w.DriverA.Id}")).StatusCode);

        await using var db = _db.CreateContext();
        Assert.True(await db.CompanyMemberships.AnyAsync(m => m.UserId == w.DriverA.Id && m.IsActive));
        Assert.True((await db.Drivers.SingleAsync(d => d.Id == w.DriverProfileA)).IsActive);
    }

    // ------------------------------------------------------------------ concurrency

    private async Task<int[]> RaceStatuses(Func<int, Task<HttpResponseMessage>> request, int n)
    {
        using var gate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, n).Select(i => Task.Run(async () =>
        {
            gate.Wait();
            return (int)(await request(i)).StatusCode;
        })).ToList();
        gate.Set();
        return await Task.WhenAll(tasks);
    }

    [SqlServerFact]
    public async Task Race_ManyRequestsForTheSameDriver_ExactlyOneWins()
    {
        const int n = 8;
        var w = await CreateWorld();
        var vehicles = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            vehicles.Add(await AddVehicle(w.AdminA, "RV " + i));
        }

        var shipments = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            shipments.Add(await AcceptedShipment(w));
        }

        var slot = Slot(8);
        var statuses = await RaceStatuses(i => Assign(w.DispatcherA, shipments[i], w.DriverProfileA, vehicles[i], slot), n);

        Assert.Equal(1, statuses.Count(s => s == 201));
        Assert.Equal(n - 1, statuses.Count(s => s == 409));
        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.DriverId == w.DriverProfileA && a.Status == AssignmentStatus.Active));
    }

    [SqlServerFact]
    public async Task Race_ManyRequestsForTheSameVehicle_ExactlyOneWins()
    {
        const int n = 8;
        var w = await CreateWorld();
        var drivers = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            drivers.Add(await AddDriver(w, "RL-" + i));
        }

        var shipments = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            shipments.Add(await AcceptedShipment(w));
        }

        var slot = Slot(8);
        var statuses = await RaceStatuses(i => Assign(w.DispatcherA, shipments[i], drivers[i], w.TruckA, slot), n);

        Assert.Equal(1, statuses.Count(s => s == 201));
        Assert.Equal(n - 1, statuses.Count(s => s == 409));
        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.VehicleId == w.TruckA && a.Status == AssignmentStatus.Active));
    }

    [SqlServerFact]
    public async Task Race_PartiallyOverlappingWindows_NeverDoubleBookTheDriver()
    {
        const int n = 6;
        var w = await CreateWorld();
        var vehicles = new List<Guid>();
        var shipments = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            vehicles.Add(await AddVehicle(w.AdminA, "PV " + i));
            shipments.Add(await AcceptedShipment(w));
        }

        // Windows [8,12) [9,13) [10,14) ... all overlap their neighbours but not necessarily each other: at most a
        // set of pairwise non-overlapping windows may win, and whatever wins must not overlap.
        var statuses = await RaceStatuses(i => Assign(w.DispatcherA, shipments[i], w.DriverProfileA, vehicles[i], Slot(8 + i, 4)), n);

        Assert.All(statuses, s => Assert.True(s is 201 or 409));
        await using var db = _db.CreateContext();
        var booked = await db.Assignments.Where(a => a.DriverId == w.DriverProfileA && a.Status == AssignmentStatus.Active).ToListAsync();
        Assert.True(booked.Count >= 1);
        Assert.Equal(statuses.Count(s => s == 201), booked.Count);
        foreach (var a in booked)
        {
            foreach (var b in booked.Where(x => x.Id != a.Id))
            {
                Assert.False(a.Window.Overlaps(b.Window), "Two active assignments of the same driver overlap.");
            }
        }
    }

    [SqlServerFact]
    public async Task Race_TwoRequestsAssigningTheSameShipment_ExactlyOneWins()
    {
        var w = await CreateWorld();
        var truck2 = await AddVehicle(w.AdminA, "TRK 2");
        var driver2 = await AddDriver(w, "LIC-A2");
        var shipment = await AcceptedShipment(w);

        var statuses = await RaceStatuses(i => i == 0
            ? Assign(w.DispatcherA, shipment, w.DriverProfileA, w.TruckA, Slot(8))
            : Assign(w.DispatcherA, shipment, driver2, truck2, Slot(8)), 2);

        Assert.Equal(1, statuses.Count(s => s == 201));
        Assert.Equal(1, statuses.Count(s => s == 409));
        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.ShipmentId == shipment && a.Status == AssignmentStatus.Active));
    }
}
