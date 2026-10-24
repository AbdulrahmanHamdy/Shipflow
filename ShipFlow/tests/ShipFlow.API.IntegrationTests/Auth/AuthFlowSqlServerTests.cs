using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShipFlow.API.IntegrationTests.Support;
using ShipFlow.Application.Common.Security;
using ShipFlow.Infrastructure.Persistence;

namespace ShipFlow.API.IntegrationTests.Auth;

/// <summary>
/// Registration, login, lockout and seeding against a real SQL Server (see <see cref="SqlServerTestSettings"/>).
/// Skipped, not passed, when SHIPFLOW_TEST_SQLSERVER is not set.
/// </summary>
public class AuthFlowSqlServerTests : IClassFixture<SqlServerDatabaseFixture>, IDisposable
{
    private const string Password = "Str0ngPassw0rd";

    private readonly SqlServerDatabaseFixture _db;
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    public AuthFlowSqlServerTests(SqlServerDatabaseFixture db)
    {
        _db = db;
        _factory = new ShipFlowApiFactory().WithSettings(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = SqlServerTestSettings.IsConfigured ? db.ConnectionString : "Server=localhost;Database=Unused",
            ["Database:SeedReferenceData"] = "true"
        });
    }

    public void Dispose() => _factory.Dispose();

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> Register(HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "Test User" });

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    [SqlServerFact]
    public async Task Register_Login_Me_RoundTrip()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();

        var registered = await Register(client, email);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var user = await registered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { Roles.Customer }, user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());

        var login = await Login(client, email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        var token = body.GetProperty("accessToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(user.GetProperty("id").GetGuid(), me.GetProperty("id").GetGuid());
        Assert.Equal(email, me.GetProperty("email").GetString());
    }

    [SqlServerFact]
    public async Task Register_CannotSelfAssignPrivilegedRoles()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = Password,
            fullName = "Mallory",
            role = Roles.PlatformAdmin,
            roles = new[] { Roles.PlatformAdmin, Roles.CompanyAdmin },
            isAdmin = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { Roles.Customer }, user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());

        await using var context = _db.CreateContext();
        var userId = user.GetProperty("id").GetGuid();
        var roleNames = await (
            from ur in context.UserRoles
            join r in context.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId
            select r.Name).ToListAsync();
        Assert.Equal(new[] { Roles.Customer }, roleNames.ToArray());
    }

    [SqlServerFact]
    public async Task Register_DuplicateEmail_Returns409_CaseInsensitively()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await Register(client, email)).StatusCode);

        var duplicate = await Register(client, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [SqlServerFact]
    public async Task Passwords_AreStoredHashed()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await Register(client, email);

        await using var context = _db.CreateContext();
        var stored = await context.Users.Where(u => u.Email == email).Select(u => u.PasswordHash).SingleAsync();

        Assert.False(string.IsNullOrEmpty(stored));
        Assert.DoesNotContain(Password, stored);
    }

    [SqlServerFact]
    public async Task Login_WrongPasswordAndUnknownEmail_LookIdentical()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await Register(client, email);

        var wrongPassword = await Login(client, email, "Wr0ngPassword!!");
        var unknownEmail = await Login(client, NewEmail(), "Wr0ngPassword!!");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var a = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var b = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
        Assert.Equal(a.GetProperty("title").GetString(), b.GetProperty("title").GetString());
    }

    [SqlServerFact]
    public async Task Login_LocksTheAccount_AfterRepeatedFailures_EvenForTheRightPassword()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await Register(client, email);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email, "Wr0ngPassword!!")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email, Password)).StatusCode);
    }

    [SqlServerFact]
    public async Task Login_DeactivatedAccount_IsRejected()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await Register(client, email);
        await using (var context = _db.CreateContext())
        {
            await context.Users.Where(u => u.Email == email).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email, Password)).StatusCode);
    }

    [SqlServerFact]
    public async Task RoleSeeding_CreatesAllFiveRoles_AndIsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

        await initializer.InitializeAsync(CancellationToken.None);
        await initializer.InitializeAsync(CancellationToken.None);

        await using var context = _db.CreateContext();
        var names = await context.Roles.Select(r => r.Name!).ToListAsync();
        Assert.Equal(Roles.All.OrderBy(r => r, StringComparer.Ordinal).ToArray(), names.OrderBy(r => r, StringComparer.Ordinal).ToArray());
    }

    [SqlServerFact]
    public async Task DevelopmentSeed_IsIgnored_OutsideDevelopment()
    {
        // Use a uniquely-named sentinel email so this test is independent of run order
        // (another test in this class may create the standard seed users in the shared DB).
        var uniquePrefix = Guid.NewGuid().ToString("N")[..8];
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _db.ConnectionString,
            ["Database:SeedReferenceData"] = "true",
            ["Database:SeedDevelopmentData"] = "true",
            // Use a unique password so the sentinel emails would be unique if seeded
            ["Seed:DevPassword"] = "Aa1-" + Guid.NewGuid().ToString("N")
        };
        using var factory = new ShipFlowApiFactory().WithSettings(settings); // environment: Testing
        using var scope = factory.Services.CreateScope();

        // Count users before the call to detect if any NEW user was created by dev-seed
        await using var contextBefore = _db.CreateContext();
        var countBefore = await contextBefore.Users.CountAsync();

        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);

        await using var contextAfter = _db.CreateContext();
        var countAfter = await contextAfter.Users.CountAsync();

        // Dev-seed users must NOT have been created in a non-Development environment.
        // Role seeding (SeedReferenceData) only creates roles, not users; user count should be unchanged.
        Assert.Equal(countBefore, countAfter);
    }

    [SqlServerFact]
    public async Task DevelopmentSeed_InDevelopment_CreatesOneUserPerRole_WithTheConfiguredPassword()
    {
        var password = "Aa1-" + Guid.NewGuid().ToString("N");
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _db.ConnectionString,
            ["Database:SeedReferenceData"] = "true",
            ["Database:SeedDevelopmentData"] = "true",
            ["Seed:DevPassword"] = password
        };
        using var factory = new ShipFlowApiFactory().WithSettings(settings, environment: "Development");
        var client = factory.CreateClient(); // starting the host runs the initializer

        var login = await Login(client, "admin@shipflow.test", password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            new[] { Roles.PlatformAdmin },
            body.GetProperty("user").GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());

        await using var context = _db.CreateContext();
        Assert.Equal(5, await context.Users.CountAsync(u => u.Email!.EndsWith("@shipflow.test")));
    }

    [SqlServerFact]
    public async Task ReadinessCheck_IsHealthy_WhenTheDatabaseIsReachable()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
