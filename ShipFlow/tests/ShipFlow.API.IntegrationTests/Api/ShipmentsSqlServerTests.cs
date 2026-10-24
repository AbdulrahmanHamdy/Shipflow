using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ShipFlow.API.IntegrationTests.Support;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Shipments;
using static ShipFlow.API.IntegrationTests.Support.PersistenceData;

namespace ShipFlow.API.IntegrationTests.Api;

/// <summary>
/// Shipment and quotation workflows over HTTP against a real SQL Server. Skipped (not passed) without
/// SHIPFLOW_TEST_SQLSERVER. Users are inserted directly and authenticate with locally signed tokens, so these tests
/// exercise authorization, ownership, tenant isolation, paging and the quote-acceptance race, not the login flow.
/// </summary>
public class ShipmentsSqlServerTests : IClassFixture<SqlServerDatabaseFixture>, IDisposable
{
    private readonly SqlServerDatabaseFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public ShipmentsSqlServerTests(SqlServerDatabaseFixture db)
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

    private sealed record Setup(Actor Customer, Actor OtherCustomer, Actor DispatcherA, Actor DispatcherB, Actor Driver, Actor Admin);

    private async Task<Setup> CreateWorld()
    {
        var customer = NewUser("customer");
        var other = NewUser("other");
        var driver = NewUser("driver");
        var admin = NewUser("admin");
        var (companyA, adminA) = NewCompany("Carrier A");
        var (companyB, adminB) = NewCompany("Carrier B");
        var dispatcherA = NewUser("dispatcherA");
        var dispatcherB = NewUser("dispatcherB");
        companyA.AddMember(dispatcherA.Id, CompanyRole.Dispatcher, Now);
        companyA.AddMember(driver.Id, CompanyRole.Driver, Now);
        companyB.AddMember(dispatcherB.Id, CompanyRole.Dispatcher, Now);

        await using var db = _db.CreateContext();
        db.Users.AddRange(customer, other, driver, admin, adminA, adminB, dispatcherA, dispatcherB);
        db.TransportCompanies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();

        return new Setup(
            new Actor(customer.Id, Roles.Customer), new Actor(other.Id, Roles.Customer),
            new Actor(dispatcherA.Id, Roles.Dispatcher, companyA.Id), new Actor(dispatcherB.Id, Roles.Dispatcher, companyB.Id),
            new Actor(driver.Id, Roles.Driver, companyA.Id), new Actor(admin.Id, Roles.PlatformAdmin));
    }

    private HttpClient As(Actor actor)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", actor.Token);
        return client;
    }

    private static object NewShipmentBody(string reference = "REF-1") => new
    {
        reference,
        cargo = new { description = "Pallets of tiles", weightKg = 1200.5, volumeM3 = 8.25 },
        stops = new object[]
        {
            new { address = "1 Pickup Street", latitude = 30.04, longitude = 31.23, windowStartUtc = "2031-01-02T08:00:00Z", windowEndUtc = "2031-01-02T10:00:00Z" },
            new { address = "9 Delivery Avenue", latitude = 31.2, longitude = 29.9 }
        }
    };

    private static object Offer(decimal amount) => new { amount, currency = "USD", expiresAtUtc = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ssZ"), terms = "Net 30" };

    private static async Task<Guid> Id(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    private async Task<Guid> CreateOpenShipment(Setup w, string reference = "REF-1")
    {
        var created = await As(w.Customer).PostAsJsonAsync("/api/shipments", NewShipmentBody(reference));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = await Id(created);
        Assert.Equal(HttpStatusCode.OK, (await As(w.Customer).PostAsync($"/api/shipments/{id}/open-for-quotes", null)).StatusCode);
        return id;
    }

    [SqlServerFact]
    public async Task FullQuotationFlow_CreateOpenQuoteAccept()
    {
        var w = await CreateWorld();
        var id = await CreateOpenShipment(w);

        var quoteA = await As(w.DispatcherA).PostAsJsonAsync($"/api/shipments/{id}/quotes", Offer(1500.25m));
        var quoteB = await As(w.DispatcherB).PostAsJsonAsync($"/api/shipments/{id}/quotes", Offer(1400m));
        Assert.Equal(HttpStatusCode.Created, quoteA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, quoteB.StatusCode);
        var quoteAId = await Id(quoteA);

        var asOwner = await As(w.Customer).GetFromJsonAsync<JsonElement[]>($"/api/shipments/{id}/quotes");
        Assert.Equal(2, asOwner!.Length);
        var asCompanyB = await As(w.DispatcherB).GetFromJsonAsync<JsonElement[]>($"/api/shipments/{id}/quotes");
        Assert.Single(asCompanyB!);

        var accepted = await As(w.Customer).PostAsync($"/api/shipments/{id}/quotes/{quoteAId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var body = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Accepted", body.GetProperty("status").GetString());
        Assert.Equal(1500.25m, body.GetProperty("agreedAmount").GetDecimal());
        Assert.Equal(w.DispatcherA.CompanyId, body.GetProperty("carrierCompanyId").GetGuid());

        // The losing company no longer sees the shipment at all.
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.DispatcherB).GetAsync($"/api/shipments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await As(w.DispatcherA).GetAsync($"/api/shipments/{id}")).StatusCode);
    }

    [SqlServerFact]
    public async Task OtherUsers_CannotSeeOrChangeAShipment_AndTheLookLikeNotFound()
    {
        var w = await CreateWorld();
        var id = await CreateOpenShipment(w);

        Assert.Equal(HttpStatusCode.NotFound, (await As(w.OtherCustomer).GetAsync($"/api/shipments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.OtherCustomer).PostAsync($"/api/shipments/{id}/open-for-quotes", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(w.OtherCustomer).PostAsJsonAsync($"/api/shipments/{id}/cancel", new { reason = "x" })).StatusCode);

        var list = await As(w.OtherCustomer).GetFromJsonAsync<JsonElement>("/api/shipments");
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    [SqlServerFact]
    public async Task RoleBoundaries_AreEnforcedOnEveryAction()
    {
        var w = await CreateWorld();
        var id = await CreateOpenShipment(w);

        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.DispatcherA).PostAsJsonAsync("/api/shipments", NewShipmentBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.Customer).PostAsJsonAsync($"/api/shipments/{id}/quotes", Offer(10m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.Driver).PostAsJsonAsync($"/api/shipments/{id}/quotes", Offer(10m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(w.Admin).PostAsJsonAsync("/api/shipments", NewShipmentBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/shipments")).StatusCode);
    }

    [SqlServerFact]
    public async Task CompanyCannotQuoteOnAShipmentThatIsNotOpen()
    {
        var w = await CreateWorld();
        var created = await As(w.Customer).PostAsJsonAsync("/api/shipments", NewShipmentBody());
        var id = await Id(created); // still Pending

        var response = await As(w.DispatcherA).PostAsJsonAsync($"/api/shipments/{id}/quotes", Offer(10m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerFact]
    public async Task TwoSimultaneousAcceptances_OnlyOneWins_AndTheDatabaseHoldsOneAcceptedQuote()
    {
        var w = await CreateWorld();
        var id = await CreateOpenShipment(w);
        var quoteA = await Id(await As(w.DispatcherA).PostAsJsonAsync($"/api/shipments/{id}/quotes", Offer(100m)));
        var quoteB = await Id(await As(w.DispatcherB).PostAsJsonAsync($"/api/shipments/{id}/quotes", Offer(90m)));
        var customer = As(w.Customer);

        var results = await Task.WhenAll(
            customer.PostAsync($"/api/shipments/{id}/quotes/{quoteA}/accept", null),
            As(w.Customer).PostAsync($"/api/shipments/{id}/quotes/{quoteB}/accept", null));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Quotes.CountAsync(q => q.ShipmentId == id && q.Status == QuoteStatus.Accepted));
        Assert.Equal(1, await db.Quotes.CountAsync(q => q.ShipmentId == id && q.Status == QuoteStatus.Rejected));
    }

    [SqlServerFact]
    public async Task List_PagesAndFilters_AndCompaniesBrowseOpenShipments()
    {
        var w = await CreateWorld();
        for (var i = 1; i <= 5; i++)
        {
            await As(w.Customer).PostAsJsonAsync("/api/shipments", NewShipmentBody($"BATCH-{i}"));
        }

        var open = await CreateOpenShipment(w, "OPEN-ONE");
        var customer = As(w.Customer);

        var page1 = await customer.GetFromJsonAsync<JsonElement>("/api/shipments?pageSize=4&page=1");
        var page2 = await customer.GetFromJsonAsync<JsonElement>("/api/shipments?pageSize=4&page=2");
        Assert.Equal(6, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page2.GetProperty("items").GetArrayLength());

        var quoting = await customer.GetFromJsonAsync<JsonElement>("/api/shipments?statuses=Quoting");
        Assert.Equal(open, Assert.Single(quoting.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        var search = await customer.GetFromJsonAsync<JsonElement>("/api/shipments?search=BATCH-3");
        Assert.Equal(1, search.GetProperty("totalCount").GetInt32());

        var marketplace = await As(w.DispatcherA).GetFromJsonAsync<JsonElement>("/api/shipments?scope=OpenForQuotes");
        Assert.Equal(open, Assert.Single(marketplace.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(0, (await As(w.DispatcherA).GetFromJsonAsync<JsonElement>("/api/shipments")).GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await customer.GetAsync("/api/shipments?pageSize=1000")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/shipments?scope=OpenForQuotes")).StatusCode);
    }

    [SqlServerFact]
    public async Task Create_RejectsBadInput_WithValidationErrors()
    {
        var w = await CreateWorld();
        var client = As(w.Customer);

        var oneStop = await client.PostAsJsonAsync("/api/shipments", new
        {
            cargo = new { description = "x", weightKg = 1, volumeM3 = 1 },
            stops = new object[] { new { address = "only", latitude = 1, longitude = 1 } }
        });
        var noWindow = await client.PostAsJsonAsync("/api/shipments", new
        {
            cargo = new { description = "x", weightKg = 1, volumeM3 = 1 },
            stops = new object[] { new { address = "a", latitude = 1, longitude = 1 }, new { address = "b", latitude = 2, longitude = 2 } }
        });
        var badLatitude = await client.PostAsJsonAsync("/api/shipments", new
        {
            cargo = new { description = "x", weightKg = 1, volumeM3 = 1 },
            stops = new object[]
            {
                new { address = "a", latitude = 91, longitude = 1, windowStartUtc = "2031-01-02T08:00:00Z", windowEndUtc = "2031-01-02T10:00:00Z" },
                new { address = "b", latitude = 2, longitude = 2 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, oneStop.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noWindow.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badLatitude.StatusCode);
    }

    [SqlServerFact]
    public async Task ThereIsNoGenericStatusUpdate()
    {
        var w = await CreateWorld();
        var id = await CreateOpenShipment(w);
        var client = As(w.Customer);

        var put = await client.PutAsJsonAsync($"/api/shipments/{id}", new { status = "Delivered" });
        var patch = await client.PatchAsJsonAsync($"/api/shipments/{id}", new { status = "Delivered" });
        var post = await client.PostAsJsonAsync($"/api/shipments/{id}/status", new { status = "Delivered" });

        Assert.False(put.IsSuccessStatusCode);
        Assert.False(patch.IsSuccessStatusCode);
        Assert.False(post.IsSuccessStatusCode);
        var shipment = await client.GetFromJsonAsync<JsonElement>($"/api/shipments/{id}");
        Assert.Equal("Quoting", shipment.GetProperty("status").GetString());
    }

    [SqlServerFact]
    public async Task Cancel_ByCustomer_Works_AndCannotBeRepeated()
    {
        var w = await CreateWorld();
        var id = await CreateOpenShipment(w);
        var client = As(w.Customer);

        var first = await client.PostAsJsonAsync($"/api/shipments/{id}/cancel", new { reason = "changed plans" });
        var second = await client.PostAsJsonAsync($"/api/shipments/{id}/cancel", new { reason = "again" });
        var noReason = await client.PostAsJsonAsync($"/api/shipments/{id}/cancel", new { reason = "" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
    }
}
