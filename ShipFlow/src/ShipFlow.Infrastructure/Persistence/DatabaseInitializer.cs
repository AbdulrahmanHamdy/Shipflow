using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.ValueObjects;
using ShipFlow.Infrastructure.Identity;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>Optional startup work: migrate (dev), seed roles, seed demo users (dev only).</summary>
public sealed partial class DatabaseInitializer
{
    /// <summary>Configuration key holding the demo accounts' password. Never committed; see docs/SETUP.md.</summary>
    public const string DevPasswordKey = "Seed:DevPassword";

    private readonly AppDbContext _db;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IOptions<DatabaseOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        AppDbContext db,
        RoleManager<ApplicationRole> roles,
        UserManager<ApplicationUser> users,
        IOptions<DatabaseOptions> options,
        IHostEnvironment environment,
        IConfiguration configuration,
        TimeProvider time,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _roles = roles;
        _users = users;
        _options = options;
        _environment = environment;
        _configuration = configuration;
        _time = time;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;

        if (options.MigrateOnStartup)
        {
            await MigrateAsync(cancellationToken);
        }

        if (options.SeedReferenceData)
        {
            await SeedRolesAsync();
        }

        if (options.SeedDevelopmentData)
        {
            await SeedDevelopmentUsersAsync();
            await SeedDevelopmentCompanyAsync(cancellationToken);
            try
            {
                await new DemoDataSeeder(_db, _users, _time, _logger).SeedAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DemoDataSeeder encountered an unhandled exception: {Message}", ex.Message);
            }
        }
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if (_db.Database.GetMigrations().Any())
        {
            await _db.Database.MigrateAsync(cancellationToken);
            return;
        }

        if (_environment.IsDevelopment())
        {
            // No migration has been generated yet: create the schema from the model so local runs work.
            LogNoMigrations();
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        throw new InvalidOperationException("No EF migrations exist; generate them before running outside Development (docs/SETUP.md).");
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (await _roles.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await _roles.CreateAsync(new ApplicationRole(role) { Id = Guid.NewGuid() });
            if (!result.Succeeded && !await _roles.RoleExistsAsync(role))
            {
                throw new InvalidOperationException($"Could not create role '{role}': " + string.Join("; ", result.Errors.Select(e => e.Code)));
            }
        }
    }

    private async Task SeedDevelopmentUsersAsync()
    {
        var password = _configuration[DevPasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            LogDevSeedSkipped(DevPasswordKey);
            return;
        }

        var demo = new (string Email, string Name, string Role)[]
        {
            ("admin@shipflow.test", "Platform Admin", Roles.PlatformAdmin),
            ("customer@shipflow.test", "Demo Customer", Roles.Customer),
            ("company.admin@shipflow.test", "Demo Company Admin", Roles.CompanyAdmin),
            ("dispatcher@shipflow.test", "Demo Dispatcher", Roles.Dispatcher),
            ("driver@shipflow.test", "Demo Driver", Roles.Driver)
        };

        foreach (var (email, name, role) in demo)
        {
            if (await _users.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = name,
                IsActive = true,
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime
            };

            var created = await _users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed '{email}': " + string.Join("; ", created.Errors.Select(e => e.Code)));
            }

            await _users.AddToRoleAsync(user, role);
        }
    }

    /// <summary>
    /// Development only: a demo company run by the seeded company admin, with the seeded dispatcher and driver as members
    /// (their Identity roles were granted when the users were created, matching these memberships), one driver profile and
    /// one vehicle. Idempotent: skipped when any company exists.
    /// </summary>
    private async Task SeedDevelopmentCompanyAsync(CancellationToken cancellationToken)
    {
        if (await _db.TransportCompanies.AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = await _users.FindByEmailAsync("company.admin@shipflow.test");
        var dispatcher = await _users.FindByEmailAsync("dispatcher@shipflow.test");
        var driverUser = await _users.FindByEmailAsync("driver@shipflow.test");
        if (admin is null || dispatcher is null || driverUser is null)
        {
            return;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var company = TransportCompany.Create("Demo Logistics", admin.Id, now);
        company.AddMember(dispatcher.Id, CompanyRole.Dispatcher, now);
        company.AddMember(driverUser.Id, CompanyRole.Driver, now);
        var driver = company.RegisterDriver(driverUser.Id, driverUser.FullName, "DEMO-0001");
        var vehicle = company.RegisterVehicle("DEMO 001", VehicleType.Truck, Capacity.Of(10_000m, 60m));

        _db.TransportCompanies.Add(company);
        _db.Drivers.Add(driver);
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No EF migrations found; creating the schema with EnsureCreated (Development only).")]
    private partial void LogNoMigrations();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Development seed skipped: configuration key {Key} is not set.")]
    private partial void LogDevSeedSkipped(string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database:SeedDevelopmentData is ignored in the {Environment} environment.")]
    private partial void LogDevSeedIgnored(string environment);
}
