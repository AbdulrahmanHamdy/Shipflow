using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tests.Support;

/// <summary>
/// Builds a consistent scenario (customer, carrier company with driver/vehicle, shipment) and can advance the
/// shipment to any status using only the public domain API, so tests never poke at internal state.
/// </summary>
public sealed class Fixture
{
    public static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    public Guid CustomerId { get; } = Guid.NewGuid();
    public Guid AdminUserId { get; } = Guid.NewGuid();
    public Guid DispatcherUserId { get; } = Guid.NewGuid();
    public Guid DriverUserId { get; } = Guid.NewGuid();

    public TransportCompany Company { get; }
    public Driver Driver { get; }
    public Vehicle Vehicle { get; }
    public Shipment Shipment { get; private set; }
    public Quote? AcceptedQuote { get; private set; }
    public Assignment? Assignment { get; private set; }

    public Fixture(ShipmentStatus status = ShipmentStatus.Pending, decimal cargoKg = 500m, decimal cargoM3 = 5m)
    {
        Company = TransportCompany.Create("Acme Logistics", AdminUserId, Now);
        Company.AddMember(DriverUserId, CompanyRole.Driver, Now);
        Company.AddMember(DispatcherUserId, CompanyRole.Dispatcher, Now);
        Driver = Company.RegisterDriver(DriverUserId, "Dana Driver", "lic-123");
        Vehicle = Company.RegisterVehicle("AB 123 CD", VehicleType.Truck, Capacity.Of(1000m, 10m));

        Shipment = Shipment.Create(CustomerId, Cargo.Of("Pallets", cargoKg, cargoM3), Stops(), Now, "REF-1");
        AdvanceTo(status);
    }

    public static DateTime T(int minutes) => Now.AddMinutes(minutes);

    public static IReadOnlyList<StopInfo> Stops() =>
    [
        new StopInfo("1 Pickup St", GeoPoint.Of(30.04, 31.23), TimeWindow.Of(T(120), T(240))),
        new StopInfo("9 Delivery Ave", GeoPoint.Of(31.20, 29.92))
    ];

    public static TimeWindow Window(int startMinutes = 120, int endMinutes = 360) =>
        TimeWindow.Of(T(startMinutes), T(endMinutes));

    public Quote SubmitQuote(Guid? companyId = null, decimal price = 250m, int expiresInMinutes = 1440) =>
        Shipment.SubmitQuote(companyId ?? Company.Id, Money.Of(price, "usd"), T(expiresInMinutes), "Net 30", T(1));

    public Assignment Assign(TimeWindow? window = null) =>
        Assignment.Create(Shipment, Driver, Vehicle, window ?? Window(), DispatcherUserId, [], T(10));

    /// <summary>Creates a second, unrelated company with its own driver and vehicle (for cross-company tests).</summary>
    public static (TransportCompany Company, Driver Driver, Vehicle Vehicle) OtherCompany()
    {
        var driverUser = Guid.NewGuid();
        var company = TransportCompany.Create("Other Carrier", Guid.NewGuid(), Now);
        company.AddMember(driverUser, CompanyRole.Driver, Now);
        var driver = company.RegisterDriver(driverUser, "Olly Other", "LIC-999");
        var vehicle = company.RegisterVehicle("ZZ 999 ZZ", VehicleType.Van, Capacity.Of(2000m, 20m));
        return (company, driver, vehicle);
    }

    private void AdvanceTo(ShipmentStatus target)
    {
        if (target == ShipmentStatus.Pending)
        {
            return;
        }

        if (target == ShipmentStatus.Cancelled)
        {
            Shipment.Cancel(CancellationInitiator.Customer, CustomerId, "changed my mind", T(2));
            return;
        }

        Shipment.OpenForQuoting(CustomerId, T(1));
        if (target == ShipmentStatus.Quoting)
        {
            return;
        }

        AcceptedQuote = SubmitQuote();
        Shipment.AcceptQuote(CustomerId, AcceptedQuote.Id, T(5));
        if (target == ShipmentStatus.Accepted)
        {
            return;
        }

        Assignment = Assign();
        if (target == ShipmentStatus.Assigned)
        {
            return;
        }

        Shipment.PickUp(DriverUserId, T(130));
        if (target == ShipmentStatus.PickedUp)
        {
            return;
        }

        Shipment.StartTransit(DriverUserId, T(140));
        if (target == ShipmentStatus.InTransit)
        {
            return;
        }

        if (target == ShipmentStatus.DeliveryFailed)
        {
            Shipment.FailDelivery(DriverUserId, "recipient absent", T(200));
            return;
        }

        Shipment.Deliver(DriverUserId, T(210));
    }
}
