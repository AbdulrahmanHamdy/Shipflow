using System.Threading.Tasks;
using ShipFlow.Infrastructure.Identity;
using ShipFlow.Infrastructure.Persistence;

namespace ShipFlow.Infrastructure.Seed;

public static class FullDevelopmentSeeder
{
    // Development seed disabled for production builds.
    public static Task SeedAsync(AppDbContext dbContext, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        return Task.CompletedTask;
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Identity;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Cargo;
using ShipFlow.Domain.Carriers;

namespace ShipFlow.Infrastructure.Seed;

/// <summary>
/// Seeds a *large* realistic development dataset.
/// This is intentionally development‑only; it is triggered by the
/// configuration flag <c>Database:FullDevelopmentSeed</c>.
/// It first clears all application tables (preserving the migration history)
/// and then inserts carriers, admins, drivers, vehicles, customers and shipments.
/// A concise report is written to the console for manual verification.
/// </summary>
public static class FullDevelopmentSeeder
{
    private static readonly string DriverPassword = "DriverTest123!";
    private static readonly string CustomerPassword = "CustomerTest123!";
    private static readonly string AdminPassword = "AdminTest123!";

    public static async Task SeedAsync(AppDbContext dbContext,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager)
    {
        // -----------------------------------------------------------------
        // 1. Clean existing data (preserve migrations)
        // -----------------------------------------------------------------
        await ClearDataAsync(dbContext);

        // -----------------------------------------------------------------
        // 2. Seed roles (if not present)
        // -----------------------------------------------------------------
        var roles = new[] { "CarrierAdmin", "Dispatcher", "Driver", "Customer", "PlatformAdmin" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new AppRole { Name = r });
        }

        // -----------------------------------------------------------------
        // 3. Create carrier companies and admins
        // -----------------------------------------------------------------
        var carriers = new List<CarrierCompany>();
        var carrierAdmins = new List<AppUser>();
        for (int i = 1; i <= 5; i++)
        {
            var carrier = new CarrierCompany
            {
                Id = Guid.NewGuid(),
                Name = $"TestCarrier{i}",
                Email = $"carrier{i}@shipflow.test",
                Phone = $"+20{10000000 + i}"
            };
            carriers.Add(carrier);
            dbContext.CarrierCompanies.Add(carrier);

            var admin = new AppUser
            {
                UserName = $"carrieradmin{i}@shipflow.test",
                Email = $"carrieradmin{i}@shipflow.test",
                EmailConfirmed = true,
                PhoneNumber = $"+20{20000000 + i}"
            };
            await userManager.CreateAsync(admin, AdminPassword);
            await userManager.AddToRoleAsync(admin, "CarrierAdmin");
            carrierAdmins.Add(admin);
        }

        await dbContext.SaveChangesAsync();

        // -----------------------------------------------------------------
        // 4. Create drivers and vehicles (40 each)
        // -----------------------------------------------------------------
        var vehicleSpecs = new[]
        {
            new { Maker = "Hyundai", Model = "H‑1", Payload = 1500M, Len = 4.5M, Wid = 2.0M, Hgt = 2.2M, DoorW = 2.0M, DoorH = 2.2M },
            new { Maker = "Toyota", Model = "HiAce", Payload = 1300M, Len = 4.0M, Wid = 1.9M, Hgt = 2.0M, DoorW = 1.9M, DoorH = 2.0M },
            new { Maker = "Mercedes‑Benz", Model = "Sprinter", Payload = 1800M, Len = 5.0M, Wid = 2.1M, Hgt = 2.3M, DoorW = 2.1M, DoorH = 2.3M },
            new { Maker = "Chevrolet", Model = "N300", Payload = 1200M, Len = 3.8M, Wid = 1.8M, Hgt = 1.9M, DoorW = 1.8M, DoorH = 1.9M },
            new { Maker = "Mitsubishi", Model = "Canter", Payload = 1700M, Len = 5.2M, Wid = 2.0M, Hgt = 2.2M, DoorW = 2.0M, DoorH = 2.2M },
            new { Maker = "Isuzu", Model = "NPR", Payload = 1600M, Len = 4.8M, Wid = 2.0M, Hgt = 2.1M, DoorW = 2.0M, DoorH = 2.1M },
            new { Maker = "Toyota", Model = "Hilux", Payload = 1000M, Len = 4.2M, Wid = 1.8M, Hgt = 1.9M, DoorW = 1.8M, DoorH = 1.9M }
        };

        var drivers = new List<AppUser>();
        var vehicles = new List<Vehicle>();
        var rand = new Random();

        for (int i = 1; i <= 40; i++)
        {
            // pick carrier cyclically
            var carrier = carriers[(i - 1) % carriers.Count];
            // pick vehicle spec cyclically
            var spec = vehicleSpecs[(i - 1) % vehicleSpecs.Length];
            // create vehicle
            var vehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                Manufacturer = spec.Maker,
                ModelName = spec.Model,
                Type = "Truck", // simple string, adjust if enum exists
                LicensePlate = $"TEST-{i:D3}",
                MaxPayloadKg = spec.Payload,
                CargoLengthM = spec.Len,
                CargoWidthM = spec.Wid,
                CargoHeightM = spec.Hgt,
                DoorWidthM = spec.DoorW,
                DoorHeightM = spec.DoorH,
                IsAvailable = true,
                CarrierCompanyId = carrier.Id
            };
            vehicles.Add(vehicle);
            dbContext.Vehicles.Add(vehicle);

            // create driver user
            var driver = new AppUser
            {
                UserName = $"driver{i:D2}@shipflow.test",
                Email = $"driver{i:D2}@shipflow.test",
                EmailConfirmed = true,
                PhoneNumber = $"+20{30000000 + i}"
            };
            await userManager.CreateAsync(driver, DriverPassword);
            await userManager.AddToRoleAsync(driver, "Driver");
            drivers.Add(driver);

            // create driver profile (domain entity)
            var driverProfile = new Driver
            {
                Id = Guid.NewGuid(),
                UserId = driver.Id,
                CarrierCompanyId = carrier.Id,
                AssignedVehicleId = vehicle.Id,
                IsAvailable = true,
                Status = DriverStatus.Active // adjust enum name if different
            };
            dbContext.Drivers.Add(driverProfile);
        }

        await dbContext.SaveChangesAsync();

        // -----------------------------------------------------------------
        // 5. Adjust availability distribution (25 available, 8 busy, 4 offline, 3 unsuitable)
        // -----------------------------------------------------------------
        // Make 8 drivers busy + their vehicles busy
        var busyDrivers = drivers.Take(8).ToList();
        foreach (var d in busyDrivers)
        {
            var profile = await dbContext.Drivers.FirstAsync(x => x.UserId == d.Id);
            profile.IsAvailable = false;
            var veh = await dbContext.Vehicles.FirstAsync(v => v.Id == profile.AssignedVehicleId);
            veh.IsAvailable = false;
        }

        // Make 4 drivers offline (IsAvailable flag stays true but we set a fake status)
        var offlineDrivers = drivers.Skip(8).Take(4).ToList();
        foreach (var d in offlineDrivers)
        {
            var profile = await dbContext.Drivers.FirstAsync(x => x.UserId == d.Id);
            profile.Status = DriverStatus.Offline; // enum placeholder
        }

        // Make 3 drivers have vehicles that cannot carry the largest cargo (reduce payload)
        var unsuitableDrivers = drivers.Skip(12).Take(3).ToList();
        foreach (var d in unsuitableDrivers)
        {
            var veh = await dbContext.Vehicles.FirstAsync(v => v.AssignedDriverId == d.Id);
            veh.MaxPayloadKg = 500; // too small for large shipments
        }

        await dbContext.SaveChangesAsync();

        // -----------------------------------------------------------------
        // 6. Create customers (25)
        // -----------------------------------------------------------------
        var customers = new List<AppUser>();
        for (int i = 1; i <= 25; i++)
        {
            var cust = new AppUser
            {
                UserName = $"customer{i:D2}@shipflow.test",
                Email = $"customer{i:D2}@shipflow.test",
                EmailConfirmed = true,
                PhoneNumber = $"+20{40000000 + i}"
            };
            await userManager.CreateAsync(cust, CustomerPassword);
            await userManager.AddToRoleAsync(cust, "Customer");
            customers.add(cust);
        }

        await dbContext.SaveChangesAsync();

        // -----------------------------------------------------------------
        // 7. Create shipments (≈ 60) with varied characteristics
        // -----------------------------------------------------------------
        var shipments = new List<Shipment>();
        var now = DateTime.UtcNow;
        var cargoId = 1;
        for (int i = 1; i <= 60; i++)
        {
            var customer = customers[rand.Next(customers.Count)];
            var shipment = new Shipment
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                CreatedAt = now.AddMinutes(-i),
                PickupLatitude = 30.0 + rand.NextDouble() * 0.2, // approx Greater Cairo
                PickupLongitude = 31.2 + rand.NextDouble() * 0.2,
                DeliveryLatitude = 30.0 + rand.NextDouble() * 0.2,
                DeliveryLongitude = 31.2 + rand.NextDouble() * 0.2,
                Status = ShipmentStatus.Pending,
                Description = $"Test shipment #{i}"
            };
            dbContext.Shipments.Add(shipment);

            // create 1‑3 cargo items per shipment
            int cargoCount = rand.Next(1, 4);
            for (int c = 0; c < cargoCount; c++)
            {
                var cargo = new CargoItem
                {
                    Id = Guid.NewGuid(),
                    ShipmentId = shipment.Id,
                    Name = $"Item {cargoId}",
                    WeightKg = rand.Next(5, 200),
                    LengthM = (decimal)(rand.NextDouble() * 2 + 0.5),
                    WidthM = (decimal)(rand.NextDouble() * 1.5 + 0.3),
                    HeightM = (decimal)(rand.NextDouble() * 1.5 + 0.3),
                    IsFragile = rand.Next(0, 5) == 0,
                    RequiresDisassembly = rand.Next(0, 10) == 0
                };
                dbContext.CargoItems.Add(cargo);
                cargoId++;
            }
        }

        await dbContext.SaveChangesAsync();

        // -----------------------------------------------------------------
        // 8. Simple report to console
        // -----------------------------------------------------------------
        Console.WriteLine("=== ShipFlow Development Seed Report ===");
        Console.WriteLine($"Carriers: {carriers.Count}");
        Console.WriteLine($"Carrier Admins: {carrierAdmins.Count}");
        Console.WriteLine($"Drivers: {drivers.Count} (Password: {DriverPassword})");
        Console.WriteLine($"Vehicles: {vehicles.Count}");
        Console.WriteLine($"Customers: {customers.Count} (Password: {CustomerPassword})");
        Console.WriteLine($"Shipments: {60}");
        Console.WriteLine("--- End of Report ---");
    }

    private static async Task ClearDataAsync(AppDbContext db)
    {
        // Order matters – delete children before parents
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [CargoItems]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Shipments]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Drivers]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Vehicles]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [CarrierCompanies]");
        // ASP.NET Identity tables
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetUserRoles]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetRoles]");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [AspNetUsers]");
    }
}
