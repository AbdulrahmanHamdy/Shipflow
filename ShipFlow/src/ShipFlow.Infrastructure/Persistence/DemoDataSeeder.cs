using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;
using ShipFlow.Domain.ValueObjects;
using ShipFlow.Infrastructure.Identity;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>
/// Seeds a comprehensive, realistic, fully interconnected demo dataset for ShipFlow:
/// multiple carriers, fleet vehicles, drivers, consignments across all statuses
/// (Pending, Quoting, Accepted, Assigned, PickedUp, InTransit, DeliveryFailed, Delivered, Cancelled),
/// with complete audit timelines, multi-carrier quotations, live GPS telemetry trails, and delivery proofs.
/// Completely idempotent: safe to execute multiple times.
/// </summary>
public sealed class DemoDataSeeder
{
    private const string DefaultDemoPassword = "DevPass1234!";
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    public DemoDataSeeder(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        TimeProvider time,
        ILogger logger)
    {
        _db = db;
        _users = users;
        _time = time;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var legacyShipmentsExist = await _db.Shipments.AnyAsync(s => s.Reference != null && s.Reference.StartsWith("SF-2026-"), cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;

        // 1. Ensure required Identity users
        var customer = await EnsureUserAsync("customer@shipflow.test", "Demo Customer", Roles.Customer, now);
        var companyAdmin = await EnsureUserAsync("company.admin@shipflow.test", "Demo Company Admin", Roles.CompanyAdmin, now);
        var dispatcher = await EnsureUserAsync("dispatcher@shipflow.test", "Demo Dispatcher", Roles.Dispatcher, now);
        var driver = await EnsureUserAsync("driver@shipflow.test", "Demo Driver", Roles.Driver, now);
        var admin = await EnsureUserAsync("admin@shipflow.test", "Platform Admin", Roles.PlatformAdmin, now);

        // Additional customers
        var acmeCustomer = await EnsureUserAsync("acme.corp@shipflow.test", "Acme Industrial Corp", Roles.Customer, now);
        var pharmaCustomer = await EnsureUserAsync("pharma.logistics@shipflow.test", "PharmaCare Global", Roles.Customer, now);

        // Additional drivers for Demo Logistics
        var driver2 = await EnsureUserAsync("driver2@shipflow.test", "Tarek Mansour", Roles.Driver, now);
        var driver3 = await EnsureUserAsync("driver3@shipflow.test", "Karim El-Sayed", Roles.Driver, now);
        var driver4 = await EnsureUserAsync("driver4@shipflow.test", "Hassan Nour", Roles.Driver, now);

        // Additional carrier: Apex Freight Lines
        var apexAdmin = await EnsureUserAsync("apex.admin@shipflow.test", "Apex Operations Admin", Roles.CompanyAdmin, now);
        var apexDispatch = await EnsureUserAsync("apex.dispatch@shipflow.test", "Apex Dispatch Desk", Roles.Dispatcher, now);
        var apexDriver = await EnsureUserAsync("apex.driver@shipflow.test", "Omar Zaid", Roles.Driver, now);

        // 2. Demo Logistics company & members
        var demoCompany = await _db.TransportCompanies
            .Include(c => c.Memberships)
            .FirstOrDefaultAsync(c => c.Name == "Demo Logistics", cancellationToken);

        if (demoCompany is null)
        {
            demoCompany = TransportCompany.Create("Demo Logistics", companyAdmin.Id, now.AddDays(-30));
            demoCompany.AddMember(dispatcher.Id, CompanyRole.Dispatcher, now.AddDays(-30));
            demoCompany.AddMember(driver.Id, CompanyRole.Driver, now.AddDays(-30));
            _db.TransportCompanies.Add(demoCompany);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Add additional driver members if not yet present
        if (!demoCompany.HasActiveMember(driver2.Id)) demoCompany.AddMember(driver2.Id, CompanyRole.Driver, now.AddDays(-20));
        if (!demoCompany.HasActiveMember(driver3.Id)) demoCompany.AddMember(driver3.Id, CompanyRole.Driver, now.AddDays(-20));
        if (!demoCompany.HasActiveMember(driver4.Id)) demoCompany.AddMember(driver4.Id, CompanyRole.Driver, now.AddDays(-20));
        await _db.SaveChangesAsync(cancellationToken);

        // Register drivers for Demo Logistics
        var drv1 = await EnsureDriverProfileAsync(demoCompany, driver.Id, "Demo Driver", "DEMO-0001", cancellationToken);
        var drv2 = await EnsureDriverProfileAsync(demoCompany, driver2.Id, "Tarek Mansour", "EGY-DRV-7892", cancellationToken);
        var drv3 = await EnsureDriverProfileAsync(demoCompany, driver3.Id, "Karim El-Sayed", "EGY-DRV-4410", cancellationToken);
        var drv4 = await EnsureDriverProfileAsync(demoCompany, driver4.Id, "Hassan Nour", "EGY-DRV-9155", cancellationToken);

        // Register vehicles for Demo Logistics
        var veh1 = await EnsureVehicleAsync(demoCompany, "DEMO001", VehicleType.Truck, Capacity.Of(10_000m, 60m),
            "Heavy Freight Truck", 6.50m, 2.40m, 2.40m, 2.30m, 2.30m, cancellationToken);
        var vehVan = await EnsureVehicleAsync(demoCompany, "DEMO-VAN-01", VehicleType.Van, Capacity.Of(1_800m, 14m),
            "Commercial Cargo Van", 3.40m, 1.80m, 1.90m, 1.60m, 1.80m, cancellationToken);
        var vehPickup = await EnsureVehicleAsync(demoCompany, "DEMO-PICKUP-02", VehicleType.Pickup, Capacity.Of(2_500m, 18m),
            "Heavy Utility Pickup", 2.20m, 1.60m, 0.90m, 1.50m, 0.90m, cancellationToken);
        var vehTruck = await EnsureVehicleAsync(demoCompany, "DEMO-TRUCK-03", VehicleType.Truck, Capacity.Of(12_000m, 75m),
            "Heavy Box Truck", 7.20m, 2.45m, 2.50m, 2.35m, 2.40m, cancellationToken);
        var vehTrailer = await EnsureVehicleAsync(demoCompany, "DEMO-TRAILER-04", VehicleType.Trailer, Capacity.Of(24_000m, 110m),
            "Articulated Freight Trailer", 13.60m, 2.45m, 2.65m, 2.40m, 2.55m, cancellationToken);

        // Assign default fleet vehicles and ensure drivers are available for dispatch
        drv1.AssignVehicle(veh1.Id); drv1.SetAvailable(true);
        drv2.AssignVehicle(vehVan.Id); drv2.SetAvailable(true);
        drv3.AssignVehicle(vehPickup.Id); drv3.SetAvailable(true);
        drv4.AssignVehicle(vehTruck.Id); drv4.SetAvailable(true);

        // 3. Apex Freight Lines company & fleet
        var apexCompany = await _db.TransportCompanies
            .Include(c => c.Memberships)
            .FirstOrDefaultAsync(c => c.Name == "Apex Freight Lines", cancellationToken);

        if (apexCompany is null)
        {
            apexCompany = TransportCompany.Create("Apex Freight Lines", apexAdmin.Id, now.AddDays(-25));
            apexCompany.AddMember(apexDispatch.Id, CompanyRole.Dispatcher, now.AddDays(-25));
            apexCompany.AddMember(apexDriver.Id, CompanyRole.Driver, now.AddDays(-25));
            _db.TransportCompanies.Add(apexCompany);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var apexDrvProfile = await EnsureDriverProfileAsync(apexCompany, apexDriver.Id, "Omar Zaid", "APEX-DRV-2001", cancellationToken);
        var apexVehTruck = await EnsureVehicleAsync(apexCompany, "APEX-TRK-101", VehicleType.Truck, Capacity.Of(15_000m, 80m),
            "Apex Freightliner Truck", 8.00m, 2.45m, 2.60m, 2.40m, 2.50m, cancellationToken);
        var apexVehVan = await EnsureVehicleAsync(apexCompany, "APEX-VAN-102", VehicleType.Van, Capacity.Of(2_000m, 15m),
            "Apex Express Van", 3.50m, 1.85m, 1.95m, 1.65m, 1.85m, cancellationToken);

        apexDrvProfile.AssignVehicle(apexVehTruck.Id);
        apexDrvProfile.SetAvailable(true);
        await _db.SaveChangesAsync(cancellationToken);

        // 4. Dedicated Driver Matching Fleet & Test Scenarios
        await SeedDriverMatchingTestDataAsync(demoCompany, apexCompany, customer, dispatcher, cancellationToken);

        if (legacyShipmentsExist)
        {
            _logger.LogInformation("Legacy demo shipments already exist; skipping legacy 28 shipment generation.");
            return;
        }

        _logger.LogInformation("Creating realistic legacy demo shipments and tracking lifecycles...");

        var noConflicts = Array.Empty<Assignment>();

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 1: IN-TRANSIT (DRIVER'S ACTIVE ASSIGNMENT - DEMO DRIVER / TRUCK DEMO001)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-1).AddHours(-4);
            var quoting = created.AddHours(1);
            var accepted = quoting.AddHours(2);
            var assigned = accepted.AddHours(1);
            var pickedUp = assigned.AddHours(1);
            var inTransit = pickedUp.AddMinutes(30);

            var stops = new List<StopInfo>
            {
                new StopInfo("Cairo Central Freight Hub, Nasr City Logistics Park, Cairo", GeoPoint.Of(30.0444, 31.2357), TimeWindow.Of(created.AddHours(2), now.AddDays(2))),
                new StopInfo("Sadat City Industrial Staging Depot, KM 92 Desert Road", GeoPoint.Of(30.3800, 30.5100)),
                new StopInfo("Alexandria Maritime Cargo Terminal 3, Port of Alexandria", GeoPoint.Of(31.2001, 29.9187), TimeWindow.Of(created.AddHours(10), now.AddDays(3)))
            };

            var s1 = Shipment.Create(customer.Id, Cargo.Of("High-Precision Medical Diagnostics & Refrigerated Sensors", 1250m, 6.5m), stops, created, "SF-2026-CAI-ALX-01");
            s1.OpenForQuoting(customer.Id, quoting);
            var q = s1.SubmitQuote(demoCompany.Id, Money.Of(1450m, "USD"), now.AddDays(5), "Net 15 days upon proof of delivery, includes thermal logging", quoting.AddMinutes(30));
            s1.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, now.AddDays(1));
            var a1 = Assignment.Create(s1, drv1, veh1, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a1);

            s1.PickUp(driver.Id, pickedUp);
            s1.StartTransit(driver.Id, inTransit);

            // Add GPS trail from Cairo along the Desert Highway to near Alexandria
            var gpsPoints = new (double Lat, double Lng, int MinutesAgo, decimal Speed)[]
            {
                (30.0444, 31.2357, 180, 0.0m),
                (30.1450, 31.0250, 155, 48.5m),
                (30.2800, 30.8200, 130, 78.0m),
                (30.4100, 30.6500, 105, 82.5m),
                (30.5200, 30.5000, 80, 65.0m),
                (30.6100, 30.4100, 60, 40.0m),
                (30.7300, 30.2900, 45, 84.0m),
                (30.8600, 30.1600, 30, 81.5m),
                (30.9800, 30.0400, 15, 79.0m),
                (31.0600, 29.8700, 8, 62.0m),
                (31.1450, 29.8950, 4, 52.5m) // Latest position reported 4 minutes ago!
            };

            foreach (var (lat, lng, minAgo, spd) in gpsPoints)
            {
                var ptTime = now.AddMinutes(-minAgo);
                _db.TrackingPoints.Add(TrackingPoint.Create(s1, driver.Id, GeoPoint.Of(lat, lng), ptTime, now, spd));
            }

            _db.Shipments.Add(s1);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 2: IN-TRANSIT (Driver 2 / Truck DEMO-TRUCK-03)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-2);
            var quoting = created.AddHours(2);
            var accepted = quoting.AddHours(3);
            var assigned = accepted.AddHours(2);
            var pickedUp = assigned.AddHours(1);
            var inTransit = pickedUp.AddHours(1);

            var stops = new List<StopInfo>
            {
                new StopInfo("6th of October Industrial District 3, Giza", GeoPoint.Of(29.9737, 30.9438), TimeWindow.Of(created.AddHours(4), now.AddDays(2))),
                new StopInfo("Suez Free Trade Logistics Interchange", GeoPoint.Of(30.1219, 31.4056)),
                new StopInfo("Ain Sokhna DP World Container Terminal, Suez", GeoPoint.Of(29.6050, 32.3360), TimeWindow.Of(created.AddHours(12), now.AddDays(2)))
            };

            var s2 = Shipment.Create(customer.Id, Cargo.Of("Industrial Automation PLCs & Variable Frequency Drives", 2800m, 12.0m), stops, created, "SF-2026-CAI-SOK-02");
            s2.OpenForQuoting(customer.Id, quoting);
            var q = s2.SubmitQuote(demoCompany.Id, Money.Of(1850m, "USD"), now.AddDays(4), "Standard commercial freight terms, tail-lift delivery", quoting.AddHours(1));
            s2.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, now.AddDays(2));
            var a2 = Assignment.Create(s2, drv2, vehTruck, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a2);

            s2.PickUp(driver2.Id, pickedUp);
            s2.StartTransit(driver2.Id, inTransit);

            var gpsPoints = new (double Lat, double Lng, int MinutesAgo, decimal Speed)[]
            {
                (29.9737, 30.9438, 120, 0.0m),
                (30.0100, 31.1500, 95, 55.0m),
                (30.0800, 31.3500, 70, 72.0m),
                (30.1219, 31.4056, 45, 60.0m),
                (29.8500, 31.9500, 25, 80.0m),
                (29.6800, 32.2200, 12, 68.0m)
            };

            foreach (var (lat, lng, minAgo, spd) in gpsPoints)
            {
                var ptTime = now.AddMinutes(-minAgo);
                _db.TrackingPoints.Add(TrackingPoint.Create(s2, driver2.Id, GeoPoint.Of(lat, lng), ptTime, now, spd));
            }

            _db.Shipments.Add(s2);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 3: PICKED UP (Driver 3 / Pickup DEMO-PICKUP-02)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-18);
            var quoting = created.AddHours(2);
            var accepted = quoting.AddHours(2);
            var assigned = accepted.AddHours(1);
            var pickedUp = now.AddHours(-2);

            var stops = new List<StopInfo>
            {
                new StopInfo("Smart Village Tech Park, Building B12, Giza", GeoPoint.Of(30.0732, 31.0180), TimeWindow.Of(created.AddHours(4), now.AddDays(1))),
                new StopInfo("Quesna Logistics Center, Menofia", GeoPoint.Of(30.5422, 31.1488), TimeWindow.Of(created.AddHours(8), now.AddDays(1)))
            };

            var s3 = Shipment.Create(customer.Id, Cargo.Of("Commercial Solar Inverters & Mounting Frameworks", 2100m, 11.5m), stops, created, "SF-2026-GZA-MNF-03");
            s3.OpenForQuoting(customer.Id, quoting);
            var q = s3.SubmitQuote(demoCompany.Id, Money.Of(950m, "USD"), now.AddDays(3), "Direct ground express dispatch", quoting.AddMinutes(45));
            s3.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, now.AddDays(1));
            var a3 = Assignment.Create(s3, drv3, vehPickup, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a3);

            s3.PickUp(driver3.Id, pickedUp);
            _db.TrackingPoints.Add(TrackingPoint.Create(s3, driver3.Id, GeoPoint.Of(30.0732, 31.0180), pickedUp, now, 0m));

            _db.Shipments.Add(s3);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 4: ASSIGNED (Awaiting pickup confirmation)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-1);
            var quoting = created.AddHours(2);
            var accepted = quoting.AddHours(3);
            var assigned = now.AddHours(-3);

            var stops = new List<StopInfo>
            {
                new StopInfo("Borg El Arab Pharma Park, Block 4, Alexandria", GeoPoint.Of(30.9167, 29.6667), TimeWindow.Of(now.AddHours(2), now.AddDays(2))),
                new StopInfo("Nasr City Distribution Depot, Cairo", GeoPoint.Of(30.0561, 31.3308), TimeWindow.Of(now.AddHours(8), now.AddDays(2)))
            };

            var s4 = Shipment.Create(customer.Id, Cargo.Of("Pharmaceutical Grade Active Ingredients (Dry Insulated)", 850m, 3.8m), stops, created, "SF-2026-ALX-CAI-04");
            s4.OpenForQuoting(customer.Id, quoting);
            var q = s4.SubmitQuote(demoCompany.Id, Money.Of(1200m, "USD"), now.AddDays(4), "Climate-controlled transit, verified dry-ice packaging", quoting.AddHours(1));
            s4.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, now.AddDays(2));
            var a4 = Assignment.Create(s4, drv3, vehVan, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a4);

            _db.Shipments.Add(s4);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 5: ASSIGNED (Driver 4 / Trailer DEMO-TRAILER-04)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-1).AddHours(-6);
            var quoting = created.AddHours(2);
            var accepted = quoting.AddHours(3);
            var assigned = now.AddHours(-4);

            var stops = new List<StopInfo>
            {
                new StopInfo("Helwan Heavy Industrial Complex, Cairo", GeoPoint.Of(29.8495, 31.3342), TimeWindow.Of(now.AddHours(4), now.AddDays(3))),
                new StopInfo("Damietta Port Grain & Heavy Cargo Terminal", GeoPoint.Of(31.4175, 31.8144), TimeWindow.Of(now.AddHours(12), now.AddDays(3)))
            };

            var s5 = Shipment.Create(customer.Id, Cargo.Of("Specialty Electrical Transformers & Copper Busbars", 5200m, 15.0m), stops, created, "SF-2026-CAI-DMY-05");
            s5.OpenForQuoting(customer.Id, quoting);
            var q = s5.SubmitQuote(demoCompany.Id, Money.Of(3200m, "USD"), now.AddDays(5), "Heavy haulage escort included, oversized permit cleared", quoting.AddMinutes(50));
            s5.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, now.AddDays(2));
            var a5 = Assignment.Create(s5, drv4, vehTrailer, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a5);

            _db.Shipments.Add(s5);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 6: ACCEPTED (AWAITING DISPATCHER ASSIGNMENT IN DEMO LOGISTICS)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-14);
            var quoting = created.AddHours(2);
            var accepted = now.AddHours(-2);

            var stops = new List<StopInfo>
            {
                new StopInfo("10th of Ramadan Automotive Tier-1 Hub", GeoPoint.Of(30.3000, 31.7500), TimeWindow.Of(now.AddHours(3), now.AddDays(2))),
                new StopInfo("Alexandria West Assembly Facility, Dekheila", GeoPoint.Of(31.1300, 29.8100), TimeWindow.Of(now.AddHours(10), now.AddDays(2)))
            };

            var s6 = Shipment.Create(customer.Id, Cargo.Of("Automotive Transmission Assemblies & Axles", 4200m, 18.0m), stops, created, "SF-2026-CAI-ALX-06");
            s6.OpenForQuoting(customer.Id, quoting);
            var q = s6.SubmitQuote(demoCompany.Id, Money.Of(2150m, "USD"), now.AddDays(4), "Secured strapping, direct express dispatch", quoting.AddHours(1));
            s6.AcceptQuote(customer.Id, q.Id, accepted);

            _db.Shipments.Add(s6);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 7: ACCEPTED (AWAITING DISPATCHER ASSIGNMENT)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-10);
            var quoting = created.AddHours(1);
            var accepted = now.AddHours(-1);

            var stops = new List<StopInfo>
            {
                new StopInfo("Suez Canal Economic Zone, North Area", GeoPoint.Of(29.9668, 32.5498), TimeWindow.Of(now.AddHours(5), now.AddDays(2))),
                new StopInfo("New Cairo Tech City Gateway", GeoPoint.Of(30.0131, 31.7456), TimeWindow.Of(now.AddHours(11), now.AddDays(2)))
            };

            var s7 = Shipment.Create(customer.Id, Cargo.Of("Heavy Duty Marine Cable Spools & Couplers", 6800m, 22.0m), stops, created, "SF-2026-SWZ-CAI-07");
            s7.OpenForQuoting(customer.Id, quoting);
            var q = s7.SubmitQuote(demoCompany.Id, Money.Of(3400m, "USD"), now.AddDays(3), "Flatbed trailer required, winch loading assistance", quoting.AddMinutes(30));
            s7.AcceptQuote(customer.Id, q.Id, accepted);

            _db.Shipments.Add(s7);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 8: QUOTING (MULTIPLE COMPETING BIDS - DEMO LOGISTICS VS APEX FREIGHT)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-8);
            var quoting = created.AddHours(1);

            var stops = new List<StopInfo>
            {
                new StopInfo("Maadi Technology Park, Building 4A, Cairo", GeoPoint.Of(29.9602, 31.2569), TimeWindow.Of(now.AddHours(6), now.AddDays(3))),
                new StopInfo("Aswan Solar Park Central Hub, Benban", GeoPoint.Of(24.0889, 32.8998), TimeWindow.Of(now.AddHours(24), now.AddDays(4)))
            };

            var s8 = Shipment.Create(customer.Id, Cargo.Of("Telecommunications Fiber Optic Drums & Splice Kits", 1600m, 8.0m), stops, created, "SF-2026-CAI-ASW-08");
            s8.OpenForQuoting(customer.Id, quoting);

            // Bids from two distinct carriers!
            s8.SubmitQuote(demoCompany.Id, Money.Of(2850m, "USD"), now.AddDays(4), "Net 30 days, Includes heavy route escorts and 24/7 telemetry", quoting.AddHours(1));
            s8.SubmitQuote(apexCompany.Id, Money.Of(2650m, "USD"), now.AddDays(3), "Net 15 days, 36h express transit guarantee", quoting.AddHours(2));

            _db.Shipments.Add(s8);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 9: QUOTING (BID FROM DEMO LOGISTICS)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-6);
            var quoting = created.AddHours(1);

            var stops = new List<StopInfo>
            {
                new StopInfo("Mehalla El-Kubra Textile Complex, Gharbia", GeoPoint.Of(30.9706, 31.1669), TimeWindow.Of(now.AddHours(8), now.AddDays(3))),
                new StopInfo("Port Said Export Docks, Terminal East", GeoPoint.Of(31.2653, 32.3019), TimeWindow.Of(now.AddHours(18), now.AddDays(3)))
            };

            var s9 = Shipment.Create(customer.Id, Cargo.Of("Organic Cotton Yarn Spools & Finished Textiles", 3100m, 24.0m), stops, created, "SF-2026-ALX-PSD-09");
            s9.OpenForQuoting(customer.Id, quoting);
            s9.SubmitQuote(demoCompany.Id, Money.Of(1750m, "USD"), now.AddDays(5), "Weatherproof tarping, Net 15 days payment terms", quoting.AddHours(1));

            _db.Shipments.Add(s9);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 10: QUOTING (FRESHLY OPENED - READY FOR DISPATCHER TO BID!)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-3);
            var quoting = created.AddMinutes(30);

            var stops = new List<StopInfo>
            {
                new StopInfo("Abu Rawash Logistics Zone, Giza", GeoPoint.Of(30.0500, 31.0800), TimeWindow.Of(now.AddHours(12), now.AddDays(3))),
                new StopInfo("Asyut University Hospital Receiving Bay, Asyut", GeoPoint.Of(27.1809, 31.1837), TimeWindow.Of(now.AddHours(24), now.AddDays(3)))
            };

            var s10 = Shipment.Create(customer.Id, Cargo.Of("Laboratory Water Filtration Assemblies", 950m, 4.5m), stops, created, "SF-2026-GZA-ASY-10");
            s10.OpenForQuoting(customer.Id, quoting);

            _db.Shipments.Add(s10);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 11: PENDING (CUSTOMER CREATED, READY TO "OPEN FOR QUOTES")
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-2);
            var stops = new List<StopInfo>
            {
                new StopInfo("Obour City Industrial Zone B, Qalyubia", GeoPoint.Of(30.2210, 31.4720), TimeWindow.Of(now.AddHours(18), now.AddDays(4))),
                new StopInfo("Ismailia Free Trade Zone, Ismailia", GeoPoint.Of(30.5965, 32.2715), TimeWindow.Of(now.AddHours(28), now.AddDays(4)))
            };

            var s11 = Shipment.Create(customer.Id, Cargo.Of("HVAC Modular Chillers & Ductwork Fittings", 2100m, 14.5m), stops, created, "SF-2026-CAI-ISZ-11");
            _db.Shipments.Add(s11);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 12: PENDING
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddHours(-1);
            var stops = new List<StopInfo>
            {
                new StopInfo("Nobariya Ceramic Belt, Beheira", GeoPoint.Of(30.6667, 30.0667), TimeWindow.Of(now.AddHours(20), now.AddDays(5))),
                new StopInfo("New Administrative Capital Central Gate, Cairo", GeoPoint.Of(30.0131, 31.7456), TimeWindow.Of(now.AddHours(30), now.AddDays(5)))
            };

            var s12 = Shipment.Create(customer.Id, Cargo.Of("Packaged Ceramic Floor Tiles (Export Quality)", 8500m, 12.0m), stops, created, "SF-2026-ALX-CAI-12");
            _db.Shipments.Add(s12);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 13: DELIVERED (BY DEMO DRIVER - IN DRIVER'S COMPLETED ASSIGNMENT HISTORY!)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-4);
            var quoting = created.AddHours(1);
            var accepted = quoting.AddHours(2);
            var assigned = accepted.AddHours(1);
            var pickedUp = assigned.AddHours(2);
            var inTransit = pickedUp.AddHours(1);
            var delivered = now.AddDays(-2).AddHours(-3);

            var stops = new List<StopInfo>
            {
                new StopInfo("Cairo Biotech Labs, Heliopolis, Cairo", GeoPoint.Of(30.0890, 31.3280), TimeWindow.Of(created.AddHours(3), delivered.AddHours(10))),
                new StopInfo("Alexandria University Medical Center, Shatby", GeoPoint.Of(31.2089, 29.9142), TimeWindow.Of(created.AddHours(8), delivered.AddHours(14)))
            };

            var s13 = Shipment.Create(customer.Id, Cargo.Of("Cold-Chain Vaccines & Biological Specimen Vials", 420m, 2.1m), stops, created, "SF-2026-CAI-ALX-13");
            s13.OpenForQuoting(customer.Id, quoting);
            var q = s13.SubmitQuote(demoCompany.Id, Money.Of(1600m, "USD"), delivered.AddDays(5), "Validated +4C temperature hold, medical courier protocol", quoting.AddMinutes(30));
            s13.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, delivered.AddHours(4));
            var a13 = Assignment.Create(s13, drv1, veh1, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a13);

            s13.PickUp(driver.Id, pickedUp);
            s13.StartTransit(driver.Id, inTransit);

            _db.TrackingPoints.Add(TrackingPoint.Create(s13, driver.Id, GeoPoint.Of(30.0890, 31.3280), pickedUp, now, 0m));
            _db.TrackingPoints.Add(TrackingPoint.Create(s13, driver.Id, GeoPoint.Of(30.6500, 30.3500), inTransit.AddHours(1), now, 82m));
            _db.TrackingPoints.Add(TrackingPoint.Create(s13, driver.Id, GeoPoint.Of(31.2089, 29.9142), delivered, now, 0m));

            var proof = DeliveryProof.Create(s13, driver.Id, "Dr. Sherif Abdel-Rahman", delivered, now,
                GeoPoint.Of(31.2089, 29.9142),
                "Temperature logger confirmed +4.1C intact. Receiving dock signature verified.",
                "sig_cai_alx_13_dr_sherif.png",
                "pod_temp_log_cai_alx_13.jpg");
            _db.DeliveryProofs.Add(proof);

            s13.Deliver(driver.Id, delivered);
            a13.Complete(s13, delivered);

            _db.Shipments.Add(s13);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 14: DELIVERED (BY DEMO DRIVER - COMPLETED ASSIGNMENT 2)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-6);
            var quoting = created.AddHours(2);
            var accepted = quoting.AddHours(2);
            var assigned = accepted.AddHours(1);
            var pickedUp = assigned.AddHours(2);
            var inTransit = pickedUp.AddHours(1);
            var delivered = now.AddDays(-4).AddHours(-2);

            var stops = new List<StopInfo>
            {
                new StopInfo("Precision Tooling Works, 10th of Ramadan", GeoPoint.Of(30.3100, 31.7400), TimeWindow.Of(created.AddHours(3), delivered.AddHours(10))),
                new StopInfo("Suez Heavy Machine Works, Attaka Industrial Zone", GeoPoint.Of(29.9200, 32.4800), TimeWindow.Of(created.AddHours(8), delivered.AddHours(14)))
            };

            var s14 = Shipment.Create(customer.Id, Cargo.Of("Precision CNC Milling Cutters & Tooling Inserts", 680m, 2.9m), stops, created, "SF-2026-CAI-SWZ-14");
            s14.OpenForQuoting(customer.Id, quoting);
            var q = s14.SubmitQuote(demoCompany.Id, Money.Of(1100m, "USD"), delivered.AddDays(5), "Shock-mounted transit container, padded interior", quoting.AddMinutes(40));
            s14.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, delivered.AddHours(4));
            var a14 = Assignment.Create(s14, drv1, veh1, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a14);

            s14.PickUp(driver.Id, pickedUp);
            s14.StartTransit(driver.Id, inTransit);

            _db.TrackingPoints.Add(TrackingPoint.Create(s14, driver.Id, GeoPoint.Of(30.3100, 31.7400), pickedUp, now, 0m));
            _db.TrackingPoints.Add(TrackingPoint.Create(s14, driver.Id, GeoPoint.Of(29.9200, 32.4800), delivered, now, 0m));

            var proof = DeliveryProof.Create(s14, driver.Id, "Eng. Tamer Hegazy", delivered, now,
                GeoPoint.Of(29.9200, 32.4800),
                "Delivered to Tool Room 2. Verified zero transit vibration damage. Stamped warehouse receipt 4402.",
                "sig_swz_14_tamer.png",
                "pod_dock_swz_14.jpg");
            _db.DeliveryProofs.Add(proof);

            s14.Deliver(driver.Id, delivered);
            a14.Complete(s14, delivered);

            _db.Shipments.Add(s14);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 15: DELIVERED (BY DRIVER 2)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-7);
            var quoting = created.AddHours(2);
            var accepted = quoting.AddHours(4);
            var assigned = accepted.AddHours(2);
            var pickedUp = assigned.AddHours(3);
            var inTransit = pickedUp.AddHours(1);
            var delivered = now.AddDays(-5).AddHours(-4);

            var stops = new List<StopInfo>
            {
                new StopInfo("Al-Ahram Appliance Factory, 6th of October", GeoPoint.Of(29.9600, 30.9300), TimeWindow.Of(created.AddHours(4), delivered.AddHours(12))),
                new StopInfo("Alexandria Commercial Cold Stores, Smouha", GeoPoint.Of(31.2156, 29.9553), TimeWindow.Of(created.AddHours(10), delivered.AddHours(16)))
            };

            var s15 = Shipment.Create(customer.Id, Cargo.Of("Commercial Kitchen Refrigeration Units", 3800m, 21.0m), stops, created, "SF-2026-GZA-ALX-15");
            s15.OpenForQuoting(customer.Id, quoting);
            var q = s15.SubmitQuote(demoCompany.Id, Money.Of(2400m, "USD"), delivered.AddDays(5), "Heavy truck with hydraulic liftgate", quoting.AddHours(1));
            s15.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, delivered.AddHours(6));
            var a15 = Assignment.Create(s15, drv2, vehTruck, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a15);

            s15.PickUp(driver2.Id, pickedUp);
            s15.StartTransit(driver2.Id, inTransit);

            var proof = DeliveryProof.Create(s15, driver2.Id, "Hazem El-Shennawy", delivered, now,
                GeoPoint.Of(31.2156, 29.9553),
                "Received on loading ramp 1. Exterior and mechanicals inspected without defect.",
                "sig_alx_15_hazem.png",
                "pod_ramp_alx_15.jpg");
            _db.DeliveryProofs.Add(proof);

            s15.Deliver(driver2.Id, delivered);
            a15.Complete(s15, delivered);

            _db.Shipments.Add(s15);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 16: DELIVERED (BY DRIVER 3)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-9);
            var quoting = created.AddHours(3);
            var accepted = quoting.AddHours(3);
            var assigned = accepted.AddHours(2);
            var pickedUp = assigned.AddHours(3);
            var inTransit = pickedUp.AddHours(1);
            var delivered = now.AddDays(-7).AddHours(-1);

            var stops = new List<StopInfo>
            {
                new StopInfo("Delta Plastics Factory, Shubra El Kheima", GeoPoint.Of(30.1286, 31.2422), TimeWindow.Of(created.AddHours(4), delivered.AddHours(12))),
                new StopInfo("Tanta Central Agricultural Cooperative Depot", GeoPoint.Of(30.7865, 31.0004), TimeWindow.Of(created.AddHours(10), delivered.AddHours(16)))
            };

            var s16 = Shipment.Create(customer.Id, Cargo.Of("Agricultural Drip Irrigation Tubing & Fittings", 2400m, 14.0m), stops, created, "SF-2026-CAI-TTA-16");
            s16.OpenForQuoting(customer.Id, quoting);
            var q = s16.SubmitQuote(demoCompany.Id, Money.Of(1350m, "USD"), delivered.AddDays(5), "Enclosed flatbed, tie-downs applied", quoting.AddHours(1));
            s16.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, delivered.AddHours(6));
            var a16 = Assignment.Create(s16, drv3, vehPickup, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a16);

            s16.PickUp(driver3.Id, pickedUp);
            s16.StartTransit(driver3.Id, inTransit);

            var proof = DeliveryProof.Create(s16, driver3.Id, "Mahmoud Soliman", delivered, now,
                GeoPoint.Of(30.7865, 31.0004),
                "32 bundles of irrigation line received and inventoried by farm manager.",
                "sig_tta_16_soliman.png",
                null);
            _db.DeliveryProofs.Add(proof);

            s16.Deliver(driver3.Id, delivered);
            a16.Complete(s16, delivered);

            _db.Shipments.Add(s16);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 17: DELIVERED (BY DRIVER 4 - HEAVY TRAILER)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-12);
            var quoting = created.AddHours(2);
            var accepted = quoting.AddHours(5);
            var assigned = accepted.AddHours(2);
            var pickedUp = assigned.AddHours(3);
            var inTransit = pickedUp.AddHours(1);
            var delivered = now.AddDays(-9).AddHours(-5);

            var stops = new List<StopInfo>
            {
                new StopInfo("National Paper Mill, Mostorod", GeoPoint.Of(30.1340, 31.3050), TimeWindow.Of(created.AddHours(4), delivered.AddHours(12))),
                new StopInfo("Sadat City Packaging Materials Plant", GeoPoint.Of(30.3800, 30.5100), TimeWindow.Of(created.AddHours(10), delivered.AddHours(16)))
            };

            var s17 = Shipment.Create(customer.Id, Cargo.Of("Recycled Packaging Pulp & Corrugated Carton Rolls", 7400m, 32.0m), stops, created, "SF-2026-CAI-MNF-17");
            s17.OpenForQuoting(customer.Id, quoting);
            var q = s17.SubmitQuote(demoCompany.Id, Money.Of(2900m, "USD"), delivered.AddDays(5), "Heavy articulated trailer transit", quoting.AddHours(2));
            s17.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, delivered.AddHours(6));
            var a17 = Assignment.Create(s17, drv4, vehTrailer, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a17);

            s17.PickUp(driver4.Id, pickedUp);
            s17.StartTransit(driver4.Id, inTransit);

            var proof = DeliveryProof.Create(s17, driver4.Id, "Adel Youssef", delivered, now,
                GeoPoint.Of(30.3800, 30.5100),
                "Full trailer unloaded. Weight slip 8810 attached. BOL counter-signed.",
                "sig_mnf_17_adel.png",
                "pod_rolls_mnf_17.jpg");
            _db.DeliveryProofs.Add(proof);

            s17.Deliver(driver4.Id, delivered);
            a17.Complete(s17, delivered);

            _db.Shipments.Add(s17);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 18: DELIVERY FAILED (READY FOR RETRY DELIVERY!)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-1).AddHours(-10);
            var quoting = created.AddHours(1);
            var accepted = quoting.AddHours(2);
            var assigned = accepted.AddHours(1);
            var pickedUp = assigned.AddHours(2);
            var inTransit = pickedUp.AddHours(1);
            var failedAt = now.AddHours(-2);

            var stops = new List<StopInfo>
            {
                new StopInfo("Downtown Tech Center, Tahrir, Cairo", GeoPoint.Of(30.0444, 31.2357), TimeWindow.Of(created.AddHours(4), now.AddDays(2))),
                new StopInfo("Dokki Corporate Financial Tower, Giza", GeoPoint.Of(30.0380, 31.2110), TimeWindow.Of(created.AddHours(8), now.AddDays(2)))
            };

            var s18 = Shipment.Create(customer.Id, Cargo.Of("High-Security Network Firewall Server Blades", 540m, 2.4m), stops, created, "SF-2026-CAI-GZA-18");
            s18.OpenForQuoting(customer.Id, quoting);
            var q = s18.SubmitQuote(demoCompany.Id, Money.Of(800m, "USD"), now.AddDays(3), "Dedicated van, tamper-evident security seals", quoting.AddMinutes(30));
            s18.AcceptQuote(customer.Id, q.Id, accepted);

            var window = TimeWindow.Of(assigned, now.AddDays(2));
            var a18 = Assignment.Create(s18, drv4, vehVan, window, dispatcher.Id, noConflicts, assigned);
            _db.Assignments.Add(a18);

            s18.PickUp(driver4.Id, pickedUp);
            s18.StartTransit(driver4.Id, inTransit);
            s18.FailDelivery(driver4.Id, "Consignee facility closed after hours. Security gate locked; receiving personnel unavailable until 08:00 tomorrow.", failedAt);

            _db.Shipments.Add(s18);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 19: CANCELLED (CUSTOMER INITIATED)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-3);
            var quoting = created.AddHours(2);
            var cancelled = now.AddDays(-2);

            var stops = new List<StopInfo>
            {
                new StopInfo("Cairo International Convention Center, Nasr City", GeoPoint.Of(30.0710, 31.3120), TimeWindow.Of(created.AddHours(4), now.AddDays(5))),
                new StopInfo("Bibliotheca Alexandrina Conference Hall", GeoPoint.Of(31.2089, 29.9092), TimeWindow.Of(created.AddHours(12), now.AddDays(5)))
            };

            var s19 = Shipment.Create(customer.Id, Cargo.Of("Exhibition Booth Displays & Lighting Rigs", 1800m, 14.0m), stops, created, "SF-2026-CAI-ALX-19");
            s19.OpenForQuoting(customer.Id, quoting);
            s19.Cancel(CancellationInitiator.Customer, customer.Id, "Trade expo postponed to next quarter by event organizers.", cancelled);

            _db.Shipments.Add(s19);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENT 20: CANCELLED (CARRIER INITIATED)
        // -----------------------------------------------------------------------------------------------------------------
        {
            var created = now.AddDays(-5);
            var quoting = created.AddHours(1);
            var accepted = quoting.AddHours(2);
            var cancelled = now.AddDays(-4);

            var stops = new List<StopInfo>
            {
                new StopInfo("Shubra Mechanical Workshops, Cairo", GeoPoint.Of(30.0800, 31.2500), TimeWindow.Of(created.AddHours(4), now.AddDays(4))),
                new StopInfo("Suez Thermal Power Station Expansion Yard", GeoPoint.Of(29.9800, 32.5200), TimeWindow.Of(created.AddHours(10), now.AddDays(4)))
            };

            var s20 = Shipment.Create(customer.Id, Cargo.Of("Industrial Steam Boiler Pressure Valves", 2600m, 7.5m), stops, created, "SF-2026-CAI-SUE-20");
            s20.OpenForQuoting(customer.Id, quoting);
            var q = s20.SubmitQuote(demoCompany.Id, Money.Of(1900m, "USD"), now.AddDays(3), "Crane offload required", quoting.AddMinutes(40));
            s20.AcceptQuote(customer.Id, q.Id, accepted);
            s20.Cancel(CancellationInitiator.Carrier, demoCompany.Id, "Carrier hydraulic lift equipment underwent emergency overhaul; customer notified for re-quote.", cancelled);

            _db.Shipments.Add(s20);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENTS 21-25: ACME INDUSTRIAL CORP (CARRIED / QUOTED BY APEX FREIGHT & DEMO LOGISTICS)
        // -----------------------------------------------------------------------------------------------------------------
        {
            // 21: Quoting (Apex bid)
            var s21 = Shipment.Create(acmeCustomer.Id, Cargo.Of("Bulk Polyethylene Granules (Industrial Grade)", 9000m, 28.0m),
                new[]
                {
                    new StopInfo("Alexandria Petrochemical Complex, Amreya", GeoPoint.Of(31.0200, 29.8000), TimeWindow.Of(now.AddHours(6), now.AddDays(3))),
                    new StopInfo("10th of Ramadan Plastics Factory Zone", GeoPoint.Of(30.3000, 31.7500), TimeWindow.Of(now.AddHours(16), now.AddDays(3)))
                }, now.AddHours(-10), "SF-2026-ACM-001");
            s21.OpenForQuoting(acmeCustomer.Id, now.AddHours(-9));
            s21.SubmitQuote(apexCompany.Id, Money.Of(2200m, "USD"), now.AddDays(4), "Pneumatic tanker discharge included", now.AddHours(-8));
            _db.Shipments.Add(s21);

            // 22: InTransit (Apex Freight)
            var s22 = Shipment.Create(acmeCustomer.Id, Cargo.Of("Structural Steel I-Beams & Heavy Brackets", 14000m, 30.0m),
                new[]
                {
                    new StopInfo("Ezz Steel Mill, Ain Sokhna", GeoPoint.Of(29.6500, 32.3000), TimeWindow.Of(now.AddDays(-1), now.AddDays(2))),
                    new StopInfo("New Cairo Monorail Construction Staging Gate 6", GeoPoint.Of(30.0200, 31.4800), TimeWindow.Of(now.AddHours(8), now.AddDays(2)))
                }, now.AddDays(-1), "SF-2026-ACM-002");
            s22.OpenForQuoting(acmeCustomer.Id, now.AddDays(-1).AddHours(1));
            var q22 = s22.SubmitQuote(apexCompany.Id, Money.Of(4100m, "USD"), now.AddDays(4), "Heavy flatbed trailer with tie-down certifications", now.AddDays(-1).AddHours(2));
            s22.AcceptQuote(acmeCustomer.Id, q22.Id, now.AddDays(-1).AddHours(3));
            var a22 = Assignment.Create(s22, apexDrvProfile, apexVehTruck, TimeWindow.Of(now.AddDays(-1).AddHours(4), now.AddDays(2)), apexDispatch.Id, noConflicts, now.AddDays(-1).AddHours(4));
            _db.Assignments.Add(a22);
            s22.PickUp(apexDriver.Id, now.AddHours(-6));
            s22.StartTransit(apexDriver.Id, now.AddHours(-5));
            _db.TrackingPoints.Add(TrackingPoint.Create(s22, apexDriver.Id, GeoPoint.Of(29.6500, 32.3000), now.AddHours(-5), now, 0m));
            _db.TrackingPoints.Add(TrackingPoint.Create(s22, apexDriver.Id, GeoPoint.Of(29.8500, 31.9000), now.AddHours(-3), now, 68m));
            _db.TrackingPoints.Add(TrackingPoint.Create(s22, apexDriver.Id, GeoPoint.Of(29.9800, 31.6000), now.AddMinutes(-20), now, 72m));
            _db.Shipments.Add(s22);

            // 23: Delivered (Apex Freight)
            var s23 = Shipment.Create(acmeCustomer.Id, Cargo.Of("Industrial Air Compressors & Storage Tanks", 5600m, 22.0m),
                new[]
                {
                    new StopInfo("Suez Mechanical Yards, Port Tawfik", GeoPoint.Of(29.9500, 32.5500), TimeWindow.Of(now.AddDays(-6), now.AddDays(-3))),
                    new StopInfo("Sadat City Machinery Plant", GeoPoint.Of(30.3800, 30.5100), TimeWindow.Of(now.AddDays(-5), now.AddDays(-3)))
                }, now.AddDays(-6), "SF-2026-ACM-003");
            s23.OpenForQuoting(acmeCustomer.Id, now.AddDays(-6).AddHours(1));
            var q23 = s23.SubmitQuote(apexCompany.Id, Money.Of(3300m, "USD"), now.AddDays(2), "Certified rigging and unloading included", now.AddDays(-6).AddHours(2));
            s23.AcceptQuote(acmeCustomer.Id, q23.Id, now.AddDays(-6).AddHours(3));
            var a23 = Assignment.Create(s23, apexDrvProfile, apexVehTruck, TimeWindow.Of(now.AddDays(-6).AddHours(4), now.AddDays(-3)), apexDispatch.Id, noConflicts, now.AddDays(-6).AddHours(4));
            _db.Assignments.Add(a23);
            s23.PickUp(apexDriver.Id, now.AddDays(-5).AddHours(-4));
            s23.StartTransit(apexDriver.Id, now.AddDays(-5).AddHours(-3));
            var delTime23 = now.AddDays(-4).AddHours(-2);
            var proof23 = DeliveryProof.Create(s23, apexDriver.Id, "Eng. Magdy Nabil", delTime23, now,
                GeoPoint.Of(30.3800, 30.5100), "Receiving inspection passed. Equipment placed on foundations.", "sig_acm_03_magdy.png", null);
            _db.DeliveryProofs.Add(proof23);
            s23.Deliver(apexDriver.Id, delTime23);
            a23.Complete(s23, delTime23);
            _db.Shipments.Add(s23);

            // 24: Accepted (Apex Freight)
            var s24 = Shipment.Create(acmeCustomer.Id, Cargo.Of("Electrical Distribution Switchboards & Breakers", 3200m, 11.0m),
                new[]
                {
                    new StopInfo("ABB Factory, 10th of Ramadan", GeoPoint.Of(30.3200, 31.7300), TimeWindow.Of(now.AddHours(4), now.AddDays(2))),
                    new StopInfo("Beni Suef Industrial Zone Logistics Center", GeoPoint.Of(29.0700, 31.0900), TimeWindow.Of(now.AddHours(14), now.AddDays(2)))
                }, now.AddHours(-12), "SF-2026-ACM-004");
            s24.OpenForQuoting(acmeCustomer.Id, now.AddHours(-11));
            var q24 = s24.SubmitQuote(apexCompany.Id, Money.Of(2400m, "USD"), now.AddDays(3), "Enclosed van with anti-static straps", now.AddHours(-10));
            s24.AcceptQuote(acmeCustomer.Id, q24.Id, now.AddHours(-3));
            _db.Shipments.Add(s24);

            // 25: Pending
            var s25 = Shipment.Create(acmeCustomer.Id, Cargo.Of("Hydraulic Excavator Spare Boom Cylinders", 4100m, 8.5m),
                new[]
                {
                    new StopInfo("CAT Equipment Depot, Qalyoub", GeoPoint.Of(30.1800, 31.2000), TimeWindow.Of(now.AddHours(10), now.AddDays(3))),
                    new StopInfo("Toshka Agricultural Project Gate 1, Aswan", GeoPoint.Of(22.5000, 31.8500), TimeWindow.Of(now.AddHours(36), now.AddDays(4)))
                }, now.AddHours(-2), "SF-2026-ACM-005");
            _db.Shipments.Add(s25);
        }

        // -----------------------------------------------------------------------------------------------------------------
        // SHIPMENTS 26-28: PHARMACARE GLOBAL
        // -----------------------------------------------------------------------------------------------------------------
        {
            // 26: InTransit (Carried by Demo Logistics / Driver 3)
            var s26 = Shipment.Create(pharmaCustomer.Id, Cargo.Of("Temperature-Controlled Oncology Therapeutics (-20C Dry Ice)", 350m, 1.8m),
                new[]
                {
                    new StopInfo("EVA Pharma Production Plant, Haram, Giza", GeoPoint.Of(30.0000, 31.1800), TimeWindow.Of(now.AddDays(-1), now.AddDays(1))),
                    new StopInfo("Mansoura Oncology Center Central Receiving", GeoPoint.Of(31.0409, 31.3785), TimeWindow.Of(now.AddHours(6), now.AddDays(1)))
                }, now.AddHours(-16), "SF-2026-PHR-001");
            s26.OpenForQuoting(pharmaCustomer.Id, now.AddHours(-15));
            var q26 = s26.SubmitQuote(demoCompany.Id, Money.Of(1400m, "USD"), now.AddDays(3), "Dedicated dry-ice van with continuous probe alerts", now.AddHours(-14));
            s26.AcceptQuote(pharmaCustomer.Id, q26.Id, now.AddHours(-13));

            // Assign to vehVan and drv2 for back-to-back non-overlapping window
            var window26 = TimeWindow.Of(now.AddHours(-10), now.AddDays(1));
            // drv3 in DEMO-VAN-01
            var a26 = Assignment.Create(s26, drv3, vehVan, window26, dispatcher.Id, noConflicts, now.AddHours(-10));
            _db.Assignments.Add(a26);
            s26.PickUp(driver3.Id, now.AddHours(-5));
            s26.StartTransit(driver3.Id, now.AddHours(-4));
            _db.TrackingPoints.Add(TrackingPoint.Create(s26, driver3.Id, GeoPoint.Of(30.0000, 31.1800), now.AddHours(-4), now, 0m));
            _db.TrackingPoints.Add(TrackingPoint.Create(s26, driver3.Id, GeoPoint.Of(30.5000, 31.2500), now.AddHours(-2), now, 75m));
            _db.TrackingPoints.Add(TrackingPoint.Create(s26, driver3.Id, GeoPoint.Of(30.8500, 31.3200), now.AddMinutes(-15), now, 80m));
            _db.Shipments.Add(s26);

            // 27: Delivered (Carried by Demo Logistics / Driver 4)
            var s27 = Shipment.Create(pharmaCustomer.Id, Cargo.Of("Disposable Dialysis Filter Cartridges", 1850m, 12.0m),
                new[]
                {
                    new StopInfo("Amoun Pharmaceutical Plant, El Obour", GeoPoint.Of(30.2200, 31.4800), TimeWindow.Of(now.AddDays(-5), now.AddDays(-2))),
                    new StopInfo("Kasr Al Ainy University Hospital Warehouse, Cairo", GeoPoint.Of(30.0300, 31.2300), TimeWindow.Of(now.AddDays(-4), now.AddDays(-2)))
                }, now.AddDays(-5), "SF-2026-PHR-002");
            s27.OpenForQuoting(pharmaCustomer.Id, now.AddDays(-5).AddHours(1));
            var q27 = s27.SubmitQuote(demoCompany.Id, Money.Of(1250m, "USD"), now.AddDays(3), "Cleanroom palletized loading", now.AddDays(-5).AddHours(2));
            s27.AcceptQuote(pharmaCustomer.Id, q27.Id, now.AddDays(-5).AddHours(3));
            var a27 = Assignment.Create(s27, drv4, vehTrailer, TimeWindow.Of(now.AddDays(-5).AddHours(4), now.AddDays(-2)), dispatcher.Id, noConflicts, now.AddDays(-5).AddHours(4));
            _db.Assignments.Add(a27);
            s27.PickUp(driver4.Id, now.AddDays(-4).AddHours(-4));
            s27.StartTransit(driver4.Id, now.AddDays(-4).AddHours(-3));
            var delTime27 = now.AddDays(-3).AddHours(-2);
            var proof27 = DeliveryProof.Create(s27, driver4.Id, "Dr. Noha Fawzy", delTime27, now,
                GeoPoint.Of(30.0300, 31.2300), "Dialysis filters inspected and stocked in central clinical pharmacy.", "sig_phr_02_noha.png", null);
            _db.DeliveryProofs.Add(proof27);
            s27.Deliver(driver4.Id, delTime27);
            a27.Complete(s27, delTime27);
            _db.Shipments.Add(s27);

            // 28: Quoting (Multiple bids)
            var s28 = Shipment.Create(pharmaCustomer.Id, Cargo.Of("Automated Blood Sample Analyzers", 780m, 4.2m),
                new[]
                {
                    new StopInfo("Medical Free Zone, Nasr City", GeoPoint.Of(30.0600, 31.3300), TimeWindow.Of(now.AddHours(8), now.AddDays(3))),
                    new StopInfo("Zagazig General Hospital Clinical Labs", GeoPoint.Of(30.5877, 31.5020), TimeWindow.Of(now.AddHours(20), now.AddDays(3)))
                }, now.AddHours(-5), "SF-2026-PHR-003");
            s28.OpenForQuoting(pharmaCustomer.Id, now.AddHours(-4));
            s28.SubmitQuote(demoCompany.Id, Money.Of(1550m, "USD"), now.AddDays(4), "White-glove uncrating and placement", now.AddHours(-3));
            s28.SubmitQuote(apexCompany.Id, Money.Of(1420m, "USD"), now.AddDays(3), "Standard tailgate delivery", now.AddHours(-2));
            _db.Shipments.Add(s28);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully generated 28 demo shipments with rich interconnected lifecycles, quotes, assignments, tracking trails, and delivery proofs!");
    }

    public const string DriverTestPassword = "DriverTest123!";

    private async Task<ApplicationUser> EnsureUserAsync(string email, string fullName, string role, DateTime now, string? password = null)
    {
        var pass = password ?? DefaultDemoPassword;
        var existing = await _users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await _users.IsInRoleAsync(existing, role))
            {
                await _users.AddToRoleAsync(existing, role);
            }
            return existing;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true,
            CreatedAtUtc = now
        };

        var res = await _users.CreateAsync(user, pass);
        if (!res.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create user {email}: " + string.Join("; ", res.Errors.Select(e => e.Code)));
        }

        await _users.AddToRoleAsync(user, role);
        return user;
    }

    private async Task<Driver> EnsureDriverProfileAsync(TransportCompany company, Guid userId, string fullName, string licenseNumber, CancellationToken ct)
    {
        var existing = await _db.Drivers.FirstOrDefaultAsync(d => d.CompanyId == company.Id && d.UserId == userId, ct);
        if (existing is not null)
        {
            return existing;
        }

        var driver = company.RegisterDriver(userId, fullName, licenseNumber);
        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync(ct);
        return driver;
    }

    private async Task<Vehicle> EnsureVehicleAsync(
        TransportCompany company,
        string plateNumber,
        VehicleType type,
        Capacity capacity,
        string? modelName = null,
        decimal? cargoLengthM = null,
        decimal? cargoWidthM = null,
        decimal? cargoHeightM = null,
        decimal? doorWidthM = null,
        decimal? doorHeightM = null,
        CancellationToken ct = default)
    {
        var normalized = string.Concat(plateNumber.Trim().Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        var existing = await _db.Vehicles.FirstOrDefaultAsync(v => v.CompanyId == company.Id && v.PlateNumber == normalized, ct);
        if (existing is not null)
        {
            if (modelName != null && existing.ModelName == null)
            {
                existing.SetSpecifications(modelName, cargoLengthM, cargoWidthM, cargoHeightM, doorWidthM, doorHeightM);
                await _db.SaveChangesAsync(ct);
            }
            return existing;
        }

        var vehicle = company.RegisterVehicle(plateNumber, type, capacity);
        if (modelName != null)
        {
            vehicle.SetSpecifications(modelName, cargoLengthM, cargoWidthM, cargoHeightM, doorWidthM, doorHeightM);
        }
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);
        return vehicle;
    }

    private async Task SeedDriverMatchingTestDataAsync(
        TransportCompany demoCompany,
        TransportCompany apexCompany,
        ApplicationUser customer,
        ApplicationUser dispatcher,
        CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        _logger.LogInformation("Seeding realistic driver matching fleet, accounts, and scenarios...");

        // -----------------------------------------------------------------------------------------------------------------
        // 1. REALISTIC FLEET VEHICLES WITH COHERENT SPECIFICATIONS
        // -----------------------------------------------------------------------------------------------------------------
        // Vehicle 1: Hyundai H-1 (Van, Payload: 1,000 kg, Vol: 6.0 m³, 2.37m x 1.62m x 1.35m, Door: 1.25m x 1.20m)
        var vehH1 = await EnsureVehicleAsync(demoCompany, "EGY-VAN-H1", VehicleType.Van, Capacity.Of(1_000m, 6.0m),
            "Hyundai H-1", 2.37m, 1.62m, 1.35m, 1.25m, 1.20m, ct);

        // Vehicle 2: Toyota HiAce (Van, Payload: 1,200 kg, Vol: 8.5 m³, 2.93m x 1.54m x 1.33m, Door: 1.35m x 1.25m)
        var vehHiAce = await EnsureVehicleAsync(demoCompany, "EGY-VAN-HIA", VehicleType.Van, Capacity.Of(1_200m, 8.5m),
            "Toyota HiAce", 2.93m, 1.54m, 1.33m, 1.35m, 1.25m, ct);

        // Vehicle 3: Mercedes-Benz Sprinter (Large Van, Payload: 1,500 kg, Vol: 12.0 m³, 3.35m x 1.78m x 1.94m, Door: 1.56m x 1.84m)
        var vehSprinter = await EnsureVehicleAsync(demoCompany, "EGY-VAN-SPR", VehicleType.Van, Capacity.Of(1_500m, 12.0m),
            "Mercedes-Benz Sprinter", 3.35m, 1.78m, 1.94m, 1.56m, 1.84m, ct);

        // Vehicle 4: Mitsubishi Canter (Light Truck, Payload: 3,000 kg, Vol: 16.0 m³, 4.30m x 1.95m x 1.90m, Door: 1.85m x 1.80m)
        var vehCanter = await EnsureVehicleAsync(demoCompany, "EGY-TRK-CAN", VehicleType.Truck, Capacity.Of(3_000m, 16.0m),
            "Mitsubishi Canter", 4.30m, 1.95m, 1.90m, 1.85m, 1.80m, ct);

        // Vehicle 5: Isuzu NPR (Medium Truck, Payload: 4,000 kg, Vol: 24.0 m³, 5.20m x 2.15m x 2.15m, Door: 2.05m x 2.05m)
        var vehNpr = await EnsureVehicleAsync(demoCompany, "EGY-TRK-NPR", VehicleType.Truck, Capacity.Of(4_000m, 24.0m),
            "Isuzu NPR", 5.20m, 2.15m, 2.15m, 2.05m, 2.05m, ct);

        // Vehicle 6: Toyota Hilux (Pickup, Payload: 1,000 kg, Vol: 2.8 m³, 1.55m x 1.52m x 0.85m, Door: 1.40m x 0.85m)
        var vehHilux = await EnsureVehicleAsync(demoCompany, "EGY-PKP-HLX", VehicleType.Pickup, Capacity.Of(1_000m, 2.8m),
            "Toyota Hilux", 1.55m, 1.52m, 0.85m, 1.40m, 0.85m, ct);

        // Vehicle 7: Chevrolet N300 (Mini Van, Payload: 700 kg, Vol: 3.5 m³, 1.80m x 1.25m x 1.15m, Door: 1.05m x 1.00m)
        var vehN300 = await EnsureVehicleAsync(demoCompany, "EGY-MVAN-N3", VehicleType.Van, Capacity.Of(700m, 3.5m),
            "Chevrolet N300", 1.80m, 1.25m, 1.15m, 1.05m, 1.00m, ct);

        // Vehicle 8: Hyundai H-1 #2 (Van, Payload: 1,000 kg, Vol: 6.0 m³, 2.37m x 1.62m x 1.35m, Door: 1.25m x 1.20m)
        var vehH2 = await EnsureVehicleAsync(demoCompany, "EGY-VAN-H2", VehicleType.Van, Capacity.Of(1_000m, 6.0m),
            "Hyundai H-1", 2.37m, 1.62m, 1.35m, 1.25m, 1.20m, ct);

        // -----------------------------------------------------------------------------------------------------------------
        // 2. REALISTIC TEST DRIVERS LINKED TO IDENTITY, COMPANY, AND VEHICLES
        // -----------------------------------------------------------------------------------------------------------------
        // Driver 1: Ahmed Hassan -> Hyundai H-1 (Available)
        var uAhmed = await EnsureUserAsync("ahmed.driver@shipflow.test", "Ahmed Hassan", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uAhmed.Id)) demoCompany.AddMember(uAhmed.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvAhmed = await EnsureDriverProfileAsync(demoCompany, uAhmed.Id, "Ahmed Hassan", "EGY-LIC-1001", ct);
        drvAhmed.AssignVehicle(vehH1.Id);
        drvAhmed.SetAvailable(true);

        // Driver 2: Mohamed Ali -> Toyota HiAce (Available)
        var uMohamed = await EnsureUserAsync("mohamed.driver@shipflow.test", "Mohamed Ali", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uMohamed.Id)) demoCompany.AddMember(uMohamed.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvMohamed = await EnsureDriverProfileAsync(demoCompany, uMohamed.Id, "Mohamed Ali", "EGY-LIC-1002", ct);
        drvMohamed.AssignVehicle(vehHiAce.Id);
        drvMohamed.SetAvailable(true);

        // Driver 3: Youssef Adel -> Mercedes-Benz Sprinter (Available)
        var uYoussef = await EnsureUserAsync("youssef.driver@shipflow.test", "Youssef Adel", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uYoussef.Id)) demoCompany.AddMember(uYoussef.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvYoussef = await EnsureDriverProfileAsync(demoCompany, uYoussef.Id, "Youssef Adel", "EGY-LIC-1003", ct);
        drvYoussef.AssignVehicle(vehSprinter.Id);
        drvYoussef.SetAvailable(true);

        // Driver 4: Omar Khaled -> Mitsubishi Canter (Available)
        var uOmar = await EnsureUserAsync("omar.driver@shipflow.test", "Omar Khaled", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uOmar.Id)) demoCompany.AddMember(uOmar.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvOmar = await EnsureDriverProfileAsync(demoCompany, uOmar.Id, "Omar Khaled", "EGY-LIC-1004", ct);
        drvOmar.AssignVehicle(vehCanter.Id);
        drvOmar.SetAvailable(true);

        // Driver 5: Mahmoud Samir -> Isuzu NPR (Available)
        var uMahmoud = await EnsureUserAsync("mahmoud.driver@shipflow.test", "Mahmoud Samir", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uMahmoud.Id)) demoCompany.AddMember(uMahmoud.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvMahmoud = await EnsureDriverProfileAsync(demoCompany, uMahmoud.Id, "Mahmoud Samir", "EGY-LIC-1005", ct);
        drvMahmoud.AssignVehicle(vehNpr.Id);
        drvMahmoud.SetAvailable(true);

        // Driver 6: Karim Mostafa -> Toyota Hilux (Available - Small / open bed)
        var uKarim = await EnsureUserAsync("karim.driver@shipflow.test", "Karim Mostafa", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uKarim.Id)) demoCompany.AddMember(uKarim.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvKarim = await EnsureDriverProfileAsync(demoCompany, uKarim.Id, "Karim Mostafa", "EGY-LIC-1006", ct);
        drvKarim.AssignVehicle(vehHilux.Id);
        drvKarim.SetAvailable(true);

        // Driver 7: Amr Fathy -> Chevrolet N300 (Available - Mini van, payload 700 kg)
        var uAmr = await EnsureUserAsync("amr.driver@shipflow.test", "Amr Fathy", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uAmr.Id)) demoCompany.AddMember(uAmr.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvAmr = await EnsureDriverProfileAsync(demoCompany, uAmr.Id, "Amr Fathy", "EGY-LIC-1007", ct);
        drvAmr.AssignVehicle(vehN300.Id);
        drvAmr.SetAvailable(true);

        // Driver 8: Hassan Ibrahim -> Hyundai H-1 #2 (Offline - ready to toggle Online)
        var uHassan = await EnsureUserAsync("hassan.driver@shipflow.test", "Hassan Ibrahim", Roles.Driver, now, DriverTestPassword);
        if (!demoCompany.HasActiveMember(uHassan.Id)) demoCompany.AddMember(uHassan.Id, CompanyRole.Driver, now.AddDays(-10));
        var drvHassan = await EnsureDriverProfileAsync(demoCompany, uHassan.Id, "Hassan Ibrahim", "EGY-LIC-1008", ct);
        drvHassan.AssignVehicle(vehH2.Id);
        drvHassan.SetAvailable(false); // Offline scenario!

        await _db.SaveChangesAsync(ct);

        // -----------------------------------------------------------------------------------------------------------------
        // 3. SCENARIO TEST SHIPMENTS (IDEMPOTENT CHECK ON REFERENCE)
        // -----------------------------------------------------------------------------------------------------------------
        var noConflicts = Array.Empty<Assignment>();

        // SCENARIO 1: Small Shipment (Mini Van Suitable) -> Status: Accepted (Ready for Dispatch)
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-SMALL-01", ct))
        {
            var created = now.AddHours(-3);
            var stops = new List<StopInfo>
            {
                new StopInfo("Nasr City Commercial Tech Center, Block 7, Cairo", GeoPoint.Of(30.0560, 31.3420), TimeWindow.Of(now.AddHours(1), now.AddHours(24))),
                new StopInfo("5th Settlement Business Park, Building 14B, New Cairo", GeoPoint.Of(30.0120, 31.4390), TimeWindow.Of(now.AddHours(2), now.AddHours(36)))
            };

            var sSmall = Shipment.Create(customer.Id, Cargo.Of("Small Electronics & Office Documents", 220m, 1.5m), stops, created, "SF-TEST-SMALL-01");
            sSmall.OpenForQuoting(customer.Id, created.AddMinutes(15));
            var q = sSmall.SubmitQuote(demoCompany.Id, Money.Of(250m, "USD"), now.AddDays(3), "Standard same-day courier dispatch", created.AddMinutes(30));
            sSmall.AcceptQuote(customer.Id, q.Id, created.AddMinutes(45));
            _db.Shipments.Add(sSmall);
        }

        // SCENARIO 2: Medium Shipment (Normal Van Suitable; Excludes Mini Van & Pickup) -> Status: Accepted
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-MEDIUM-02", ct))
        {
            var created = now.AddHours(-4);
            var stops = new List<StopInfo>
            {
                new StopInfo("10th of Ramadan City Industrial Zone B3, KM 45", GeoPoint.Of(30.3010, 31.7450), TimeWindow.Of(now.AddHours(1), now.AddHours(24))),
                new StopInfo("Heliopolis Cargo Distribution Center, El-Mirghani, Cairo", GeoPoint.Of(30.0900, 31.3300), TimeWindow.Of(now.AddHours(3), now.AddHours(36)))
            };

            var sMed = Shipment.Create(customer.Id, Cargo.Of("Retail Apparel & Commercial Textiles", 850m, 5.2m), stops, created, "SF-TEST-MEDIUM-02");
            sMed.OpenForQuoting(customer.Id, created.AddMinutes(15));
            var q = sMed.SubmitQuote(demoCompany.Id, Money.Of(680m, "USD"), now.AddDays(3), "Enclosed dry van cargo transport", created.AddMinutes(30));
            sMed.AcceptQuote(customer.Id, q.Id, created.AddMinutes(45));
            _db.Shipments.Add(sMed);
        }

        // SCENARIO 3: Large Furniture Shipment (Requires Large Van or Truck) -> Status: Accepted
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-LARGE-03", ct))
        {
            var created = now.AddHours(-5);
            var stops = new List<StopInfo>
            {
                new StopInfo("Damietta Furniture Exhibition Complex, Port Said Road", GeoPoint.Of(31.4170, 31.8140), TimeWindow.Of(now.AddHours(2), now.AddHours(48))),
                new StopInfo("Sheikh Zayed Residential Compound Villa 42, Giza", GeoPoint.Of(30.0380, 30.9820), TimeWindow.Of(now.AddHours(6), now.AddHours(72)))
            };

            var sLarge = Shipment.Create(customer.Id, Cargo.Of("Handcrafted Solid Wood Living Room & 3-Door Wardrobe Suite (Max Length 3.1m)", 1350m, 10.5m), stops, created, "SF-TEST-LARGE-03");
            sLarge.OpenForQuoting(customer.Id, created.AddMinutes(20));
            var q = sLarge.SubmitQuote(demoCompany.Id, Money.Of(1450m, "USD"), now.AddDays(5), "White-glove blanket wrap and tail-lift loading", created.AddMinutes(40));
            sLarge.AcceptQuote(customer.Id, q.Id, created.AddMinutes(60));
            _db.Shipments.Add(sLarge);
        }

        // SCENARIO 4: Heavy Shipment (Requires Medium Truck) -> Status: Accepted
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-HEAVY-04", ct))
        {
            var created = now.AddHours(-6);
            var stops = new List<StopInfo>
            {
                new StopInfo("Helwan Heavy Metal Foundry & Pipe Works, South Cairo", GeoPoint.Of(29.8450, 31.3000), TimeWindow.Of(now.AddHours(2), now.AddHours(48))),
                new StopInfo("6th of October Heavy Machinery Zone, Industrial Area 3", GeoPoint.Of(29.9320, 30.9150), TimeWindow.Of(now.AddHours(5), now.AddHours(72)))
            };

            var sHeavy = Shipment.Create(customer.Id, Cargo.Of("Industrial Electric Motors, Steel Pumps & Gearboxes", 2800m, 14.0m), stops, created, "SF-TEST-HEAVY-04");
            sHeavy.OpenForQuoting(customer.Id, created.AddMinutes(20));
            var q = sHeavy.SubmitQuote(demoCompany.Id, Money.Of(1850m, "USD"), now.AddDays(4), "Heavy freight payload transport on rigid truck", created.AddMinutes(35));
            sHeavy.AcceptQuote(customer.Id, q.Id, created.AddMinutes(50));
            _db.Shipments.Add(sHeavy);
        }

        // SCENARIO 5: Oversized Freight (No Current Available Vehicle) -> Status: Accepted (for Scenario F testing)
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-OVERSIZE-05", ct))
        {
            var created = now.AddHours(-7);
            var stops = new List<StopInfo>
            {
                new StopInfo("Ain Sokhna Heavy Quarry Terminal, Red Sea Coast", GeoPoint.Of(29.6000, 32.3200), TimeWindow.Of(now.AddHours(3), now.AddHours(72))),
                new StopInfo("New Administrative Capital Ministries Complex, Cairo", GeoPoint.Of(30.0160, 31.7500), TimeWindow.Of(now.AddHours(8), now.AddHours(96)))
            };

            var sOver = Shipment.Create(customer.Id, Cargo.Of("Heavy Pre-cast Concrete Beams & Steel Reinforcements", 16500m, 45.0m), stops, created, "SF-TEST-OVERSIZE-05");
            sOver.OpenForQuoting(customer.Id, created.AddMinutes(15));
            var q = sOver.SubmitQuote(demoCompany.Id, Money.Of(3200m, "USD"), now.AddDays(5), "Heavy haulage transport", created.AddMinutes(30));
            sOver.AcceptQuote(customer.Id, q.Id, created.AddMinutes(45));
            _db.Shipments.Add(sOver);
        }

        // SCENARIO 6: Active Dispatched Shipment (Currently in SearchingForDriver status) -> Immediately open for drivers!
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-MATCHING-06", ct))
        {
            var created = now.AddHours(-2);
            var stops = new List<StopInfo>
            {
                new StopInfo("Obour Central Agricultural Wholesale Market, Gate 4", GeoPoint.Of(30.2200, 31.4700), TimeWindow.Of(now.AddHours(1), now.AddHours(12))),
                new StopInfo("Maadi Grand Fresh Market, Road 9, Cairo", GeoPoint.Of(29.9600, 31.2600), TimeWindow.Of(now.AddHours(3), now.AddHours(24)))
            };

            var sMatch = Shipment.Create(customer.Id, Cargo.Of("Packaged Organic Food Crates & Dry Groceries", 600m, 4.0m), stops, created, "SF-TEST-MATCHING-06");
            sMatch.OpenForQuoting(customer.Id, created.AddMinutes(10));
            var q = sMatch.SubmitQuote(demoCompany.Id, Money.Of(480m, "USD"), now.AddDays(2), "Expedited perishables carrier delivery", created.AddMinutes(20));
            sMatch.AcceptQuote(customer.Id, q.Id, created.AddMinutes(30));
            sMatch.MarkSearchingForDriver(dispatcher.Id, now.AddHours(-1)); // Open for matching right now!
            _db.Shipments.Add(sMatch);
        }

        // SCENARIO 7: Already Assigned Shipment (Active Assignment)
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-ASSIGNED-07", ct))
        {
            var created = now.AddHours(-8);
            var stops = new List<StopInfo>
            {
                new StopInfo("Borg El-Arab Pharma Park, Zone 2, Alexandria", GeoPoint.Of(30.9100, 29.6700), TimeWindow.Of(now.AddHours(-4), now.AddHours(20))),
                new StopInfo("Tanta University Specialized Hospital, Central Gharbia", GeoPoint.Of(30.7900, 31.0000), TimeWindow.Of(now.AddHours(2), now.AddHours(36)))
            };

            var sAssigned = Shipment.Create(customer.Id, Cargo.Of("Pharmaceutical Vaccines & Temperature Sensitive Coolers", 450m, 3.0m), stops, created, "SF-TEST-ASSIGNED-07");
            sAssigned.OpenForQuoting(customer.Id, created.AddMinutes(20));
            var q = sAssigned.SubmitQuote(demoCompany.Id, Money.Of(820m, "USD"), now.AddDays(3), "Dedicated refrigerated carrier transport", created.AddMinutes(40));
            sAssigned.AcceptQuote(customer.Id, q.Id, created.AddMinutes(60));

            // Assign to driver2 (Tarek Mansour) who is one of our busy drivers!
            var drvTarek = await _db.Drivers.FirstOrDefaultAsync(d => d.CompanyId == demoCompany.Id && d.LicenseNumber == "EGY-DRV-7892", ct);
            var vehDemoTruck = await _db.Vehicles.FirstOrDefaultAsync(v => v.CompanyId == demoCompany.Id && v.PlateNumber == "DEMO-TRUCK-03", ct);

            if (drvTarek != null && vehDemoTruck != null)
            {
                var assignWindow = TimeWindow.Of(now.AddHours(-2), now.AddHours(10));
                var a7 = Assignment.Create(sAssigned, drvTarek, vehDemoTruck, assignWindow, dispatcher.Id, noConflicts, now.AddHours(-2));
                _db.Assignments.Add(a7);
            }
            _db.Shipments.Add(sAssigned);
        }

        // SCENARIO 8: Cancelled Shipment
        if (!await _db.Shipments.AnyAsync(s => s.Reference == "SF-TEST-CANCELLED-08", ct))
        {
            var created = now.AddHours(-10);
            var stops = new List<StopInfo>
            {
                new StopInfo("Ain Shams Commercial Staging Terminal, Cairo", GeoPoint.Of(30.1300, 31.3200), TimeWindow.Of(now.AddHours(2), now.AddHours(24))),
                new StopInfo("Qalyub Industrial Warehouses, Qalyubia", GeoPoint.Of(30.1800, 31.2100), TimeWindow.Of(now.AddHours(4), now.AddHours(36)))
            };

            var sCancelled = Shipment.Create(customer.Id, Cargo.Of("Sample Ceramic Tile Display Racks", 500m, 3.5m), stops, created, "SF-TEST-CANCELLED-08");
            sCancelled.OpenForQuoting(customer.Id, created.AddMinutes(15));
            var q = sCancelled.SubmitQuote(demoCompany.Id, Money.Of(420m, "USD"), now.AddDays(2), "Standard palletized freight delivery", created.AddMinutes(30));
            sCancelled.AcceptQuote(customer.Id, q.Id, created.AddMinutes(45));
            sCancelled.Cancel(CancellationInitiator.Customer, customer.Id, "Customer rescheduled shipping date due to site renovations.", now.AddHours(-1));
            _db.Shipments.Add(sCancelled);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Successfully completed idempotent seeding of driver matching test vehicles, drivers, and scenario consignments!");
    }
}
