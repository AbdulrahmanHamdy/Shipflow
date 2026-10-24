using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;
using ShipFlow.Infrastructure.Identity;

namespace ShipFlow.API.IntegrationTests.Support;

/// <summary>Builders for persistence tests. They go through the domain's own factories, never around them.</summary>
public static class PersistenceData
{
    public static readonly DateTime Now = new(2030, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    public static ApplicationUser NewUser(string label)
    {
        var id = Guid.NewGuid();
        var name = $"{label}-{id:N}";
        return new ApplicationUser
        {
            Id = id,
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            Email = name + "@example.com",
            NormalizedEmail = (name + "@example.com").ToUpperInvariant(),
            FullName = label,
            IsActive = true,
            CreatedAtUtc = Now
        };
    }

    public static Shipment NewShipment(Guid customerId, string? reference = "REF-1")
    {
        var pickupWindow = TimeWindow.Of(Now.AddDays(1), Now.AddDays(1).AddHours(2));
        var stops = new[]
        {
            new StopInfo("1 Pickup Street", GeoPoint.Of(30.0444, 31.2357), pickupWindow),
            new StopInfo("2 Waypoint Road", GeoPoint.Of(30.5, 31.0)),
            new StopInfo("9 Delivery Avenue", GeoPoint.Of(31.2001, 29.9187))
        };
        return Shipment.Create(customerId, Cargo.Of("Pallets of tiles", 1200.5m, 8.25m), stops, Now, reference);
    }

    public static (TransportCompany Company, ApplicationUser Admin) NewCompany(string name)
    {
        var admin = NewUser("admin");
        return (TransportCompany.Create(name, admin.Id, Now), admin);
    }
}
