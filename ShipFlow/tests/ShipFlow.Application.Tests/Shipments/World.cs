using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Quotations;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Tests.Shipments;

internal sealed class FakeClock : TimeProvider
{
    public DateTime Now { get; set; } = new(2030, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    private string[] _roles = [];

    public Guid? UserId { get; private set; }

    public bool IsAuthenticated => UserId is not null;

    public string? Email => UserId is null ? null : "user@example.com";

    public IReadOnlyCollection<string> Roles => _roles;

    public bool IsInRole(string role) => _roles.Contains(role);

    public void Set(Guid? userId, params string[] roles)
    {
        UserId = userId;
        _roles = roles;
    }
}

internal sealed class FakeShipmentStore : IShipmentStore
{
    public Dictionary<Guid, Shipment> Shipments { get; } = [];

    public ShipmentListFilter? LastFilter { get; private set; }

    public Task AddAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        Shipments[shipment.Id] = shipment;
        return Task.CompletedTask;
    }

    public Task<Shipment?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Shipments.GetValueOrDefault(id));

    public Task<Shipment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Shipments.GetValueOrDefault(id));

    public Task<PagedResult<ShipmentSummaryDto>> ListAsync(ShipmentListFilter filter, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(new PagedResult<ShipmentSummaryDto>([], filter.Page, filter.PageSize, 0));
    }
}

internal sealed class FakeAssignmentStore : IAssignmentStore
{
    public List<Assignment> Assignments { get; } = [];

    /// <summary>Awaited inside the overlap read so concurrent requests really interleave in the race tests.</summary>
    public bool YieldOnRead { get; set; }

    public Task AddAsync(Assignment assignment, CancellationToken cancellationToken)
    {
        lock (Assignments)
        {
            Assignments.Add(assignment);
        }

        return Task.CompletedTask;
    }

    public Task<Assignment?> FindActiveForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot().FirstOrDefault(a => a.ShipmentId == shipmentId && a.Status == AssignmentStatus.Active));

    public Task<IReadOnlyList<Assignment>> ListActiveOverlappingAsync(Guid companyId, TimeWindow window, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Assignment>>(
            Snapshot().Where(a => a.CompanyId == companyId && a.Status == AssignmentStatus.Active && a.Window.Overlaps(window)).ToList());

    public async Task<IReadOnlyList<Assignment>> ListActiveOverlappingForAsync(
        Guid driverId, Guid vehicleId, TimeWindow window, CancellationToken cancellationToken)
    {
        var found = Snapshot()
            .Where(a => (a.DriverId == driverId || a.VehicleId == vehicleId) && a.Status == AssignmentStatus.Active && a.Window.Overlaps(window))
            .ToList();
        if (YieldOnRead)
        {
            await Task.Delay(20, cancellationToken);
        }

        return found;
    }

    public Task<bool> HasActiveForDriverAsync(Guid driverId, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot().Any(a => a.DriverId == driverId && a.Status == AssignmentStatus.Active));

    public Task<bool> HasActiveForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot().Any(a => a.VehicleId == vehicleId && a.Status == AssignmentStatus.Active));

    private List<Assignment> Snapshot()
    {
        lock (Assignments)
        {
            return Assignments.ToList();
        }
    }
}

internal sealed class FakeCompanyAccess : ICompanyAccess
{
    private readonly Dictionary<Guid, CompanyMembershipInfo> _memberships = [];

    public void Set(Guid userId, Guid companyId, CompanyRole role) => _memberships[userId] = new CompanyMembershipInfo(companyId, role);

    public Task<CompanyMembershipInfo?> FindActiveMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_memberships.GetValueOrDefault(userId));
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Exception? FailWith { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Saves++;
        return Task.CompletedTask;
    }
}

/// <summary>A small universe: two customers, two carrier companies with staff, wired to in-memory ports.</summary>
internal sealed class World
{
    public FakeClock Time { get; } = new();
    public FakeCurrentUser User { get; } = new();
    public FakeShipmentStore Store { get; } = new();
    public FakeAssignmentStore Assignments { get; } = new();
    public FakeCompanyAccess Companies { get; } = new();
    public FakeUnitOfWork Uow { get; } = new();

    public Guid Customer { get; } = Guid.NewGuid();
    public Guid OtherCustomer { get; } = Guid.NewGuid();
    public Guid PlatformAdmin { get; } = Guid.NewGuid();

    public Guid DispatcherA { get; } = Guid.NewGuid();
    public Guid DriverA { get; } = Guid.NewGuid();
    public Guid DispatcherB { get; } = Guid.NewGuid();

    public TransportCompany CompanyA { get; }
    public TransportCompany CompanyB { get; }

    public World()
    {
        CompanyA = TransportCompany.Create("Carrier A", Guid.NewGuid(), Time.Now);
        CompanyB = TransportCompany.Create("Carrier B", Guid.NewGuid(), Time.Now);
        Companies.Set(DispatcherA, CompanyA.Id, CompanyRole.Dispatcher);
        Companies.Set(DriverA, CompanyA.Id, CompanyRole.Driver);
        Companies.Set(DispatcherB, CompanyB.Id, CompanyRole.Dispatcher);
    }

    public ViewerResolver Viewers => new(User, Companies);

    public void AsCustomer(Guid? id = null) => User.Set(id ?? Customer, Roles.Customer);

    public void AsDispatcherA() => User.Set(DispatcherA, Roles.Dispatcher);

    public void AsDispatcherB() => User.Set(DispatcherB, Roles.Dispatcher);

    public void AsAdmin() => User.Set(PlatformAdmin, Roles.PlatformAdmin);

    public Shipment AddShipment(bool openForQuoting = false)
    {
        var shipment = Shipment.Create(
            Customer,
            Cargo.Of("Pallets", 500m, 5m),
            [
                new StopInfo("Pickup", GeoPoint.Of(30, 31), TimeWindow.Of(Time.Now.AddDays(1), Time.Now.AddDays(1).AddHours(2))),
                new StopInfo("Drop", GeoPoint.Of(31, 30))
            ],
            Time.Now,
            "REF");
        if (openForQuoting)
        {
            shipment.OpenForQuoting(Customer, Time.Now);
        }

        Store.Shipments[shipment.Id] = shipment;
        return shipment;
    }

    public Quote AddQuote(Shipment shipment, TransportCompany company, decimal amount = 100m, int expiresInHours = 2) =>
        shipment.SubmitQuote(company.Id, Money.Of(amount, "USD"), Time.Now.AddHours(expiresInHours), "terms", Time.Now);

    /// <summary>An Accepted shipment of company A with an active assignment, ready to be cancelled.</summary>
    public (Shipment Shipment, Assignment Assignment) AddAssignedShipment()
    {
        var shipment = AddShipment(openForQuoting: true);
        var quote = AddQuote(shipment, CompanyA);
        shipment.AcceptQuote(Customer, quote.Id, Time.Now);

        CompanyA.AddMember(DriverA, CompanyRole.Driver, Time.Now);
        var driver = CompanyA.RegisterDriver(DriverA, "Dan", "L1");
        var vehicle = CompanyA.RegisterVehicle("AB1", VehicleType.Truck, Capacity.Of(5000m, 50m));
        var assignment = Assignment.Create(
            shipment, driver, vehicle, TimeWindow.Of(Time.Now.AddDays(1), Time.Now.AddDays(1).AddHours(5)),
            DispatcherA, [], Time.Now);
        Assignments.Assignments.Add(assignment);
        return (shipment, assignment);
    }

    public CreateShipmentHandler Create() => new(new CreateShipmentCommandValidator(), User, Store, Uow, Time);
    public GetShipmentByIdHandler GetById() => new(Viewers, Store);
    public GetShipmentsHandler List() => new(new GetShipmentsQueryValidator(), Viewers, Store);
    public OpenForQuotingHandler Open() => new(User, Store, Uow, Time);
    public CancelShipmentHandler Cancel() => new(new CancelShipmentCommandValidator(), Viewers, Store, Assignments, Uow, Time);
    public SubmitQuoteHandler Submit() => new(new SubmitQuoteCommandValidator(), Viewers, Store, Uow, Time);
    public WithdrawQuoteHandler Withdraw() => new(Viewers, Store, Uow, Time);
    public GetQuotesHandler Quotes() => new(Viewers, Store);
    public AcceptQuoteHandler Accept() => new(User, Store, Uow, Time);

    public static CreateShipmentCommand ValidCreate() => new(
        "REF-9",
        new CargoDto("Boxes", 100m, 1m),
        [
            new StopInput("A street", 30, 31, new DateTime(2030, 2, 1, 8, 0, 0, DateTimeKind.Utc), new DateTime(2030, 2, 1, 10, 0, 0, DateTimeKind.Utc)),
            new StopInput("B street", 31, 30, null, null)
        ]);
}
