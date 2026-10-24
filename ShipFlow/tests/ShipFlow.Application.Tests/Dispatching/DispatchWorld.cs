using System.Collections.Concurrent;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Companies;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Application.Tests.Shipments;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Tests.Dispatching;

internal sealed class FakeCompanyStore : ICompanyStore
{
    public Dictionary<Guid, TransportCompany> Companies { get; } = [];

    public Dictionary<Guid, UserSummary> Users { get; } = [];

    public Task AddAsync(TransportCompany company, CancellationToken cancellationToken)
    {
        Companies[company.Id] = company;
        return Task.CompletedTask;
    }

    public Task<TransportCompany?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Companies.GetValueOrDefault(id));

    public Task<TransportCompany?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Companies.GetValueOrDefault(id));

    public Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid companyId, bool includeFormer, CancellationToken cancellationToken)
    {
        var members = Companies[companyId].Memberships
            .Where(m => includeFormer || m.IsActive)
            .Select(m => new MemberDto(m.UserId, Users[m.UserId].Email, Users[m.UserId].FullName, m.Role.ToString(), m.IsActive, m.JoinedAtUtc, m.LeftAtUtc))
            .ToList();
        return Task.FromResult<IReadOnlyList<MemberDto>>(members);
    }
}

internal sealed class FakeFleetStore : IFleetStore
{
    public List<Driver> Drivers { get; } = [];

    public List<Vehicle> Vehicles { get; } = [];

    public Task AddDriverAsync(Driver driver, CancellationToken cancellationToken)
    {
        Drivers.Add(driver);
        return Task.CompletedTask;
    }

    public Task AddVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        Vehicles.Add(vehicle);
        return Task.CompletedTask;
    }

    public Task<Driver?> FindDriverAsync(Guid companyId, Guid driverId, CancellationToken cancellationToken) =>
        Task.FromResult(Drivers.FirstOrDefault(d => d.CompanyId == companyId && d.Id == driverId));

    public Task<Driver?> FindDriverForUpdateAsync(Guid companyId, Guid driverId, CancellationToken cancellationToken) =>
        FindDriverAsync(companyId, driverId, cancellationToken);

    public Task<Driver?> FindDriverByUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Drivers.FirstOrDefault(d => d.CompanyId == companyId && d.UserId == userId));

    public Task<Vehicle?> FindVehicleAsync(Guid companyId, Guid vehicleId, CancellationToken cancellationToken) =>
        Task.FromResult(Vehicles.FirstOrDefault(v => v.CompanyId == companyId && v.Id == vehicleId));

    public Task<Vehicle?> FindVehicleForUpdateAsync(Guid companyId, Guid vehicleId, CancellationToken cancellationToken) =>
        FindVehicleAsync(companyId, vehicleId, cancellationToken);

    public Task<PagedResult<Driver>> ListDriversAsync(Guid companyId, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = Drivers.Where(d => d.CompanyId == companyId && (isActive is null || d.IsActive == isActive)).OrderBy(d => d.FullName).ToList();
        return Task.FromResult(new PagedResult<Driver>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count));
    }

    public Task<PagedResult<Vehicle>> ListVehiclesAsync(Guid companyId, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = Vehicles.Where(v => v.CompanyId == companyId && (isActive is null || v.IsActive == isActive)).OrderBy(v => v.PlateNumber).ToList();
        return Task.FromResult(new PagedResult<Vehicle>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count));
    }

    public Task<IReadOnlyList<Driver>> ListActiveDriversAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Driver>>(Drivers.Where(d => d.CompanyId == companyId && d.IsActive).ToList());

    public Task<IReadOnlyList<Vehicle>> ListActiveVehiclesAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Vehicle>>(Vehicles.Where(v => v.CompanyId == companyId && v.IsActive).ToList());
}

internal sealed class FakeUserDirectory : IUserDirectory
{
    public Dictionary<Guid, UserSummary> Users { get; } = [];

    public Task<UserSummary?> FindActiveByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.GetValueOrDefault(userId));

    public Task<UserSummary?> FindActiveByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
}

internal sealed class FakeMembershipRoles : IMembershipRoles
{
    public HashSet<(Guid UserId, string Role)> Granted { get; } = [];

    public Task StageGrantAsync(Guid userId, CompanyRole role, CancellationToken cancellationToken)
    {
        Granted.Add((userId, CompanyRoleMapping.ToIdentityRole(role)));
        return Task.CompletedTask;
    }

    public Task StageRevokeAsync(Guid userId, CompanyRole role, CancellationToken cancellationToken)
    {
        Granted.Remove((userId, CompanyRoleMapping.ToIdentityRole(role)));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Same contract as the SQL Server implementation: named exclusive locks taken in sorted order, held until the work ends.
/// With <see cref="Enabled"/> false it takes no locks, which the control test uses to show the race is real.
/// </summary>
internal sealed class FakeFleetTransaction : IFleetTransaction
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private readonly string _scope = Guid.NewGuid().ToString("N");

    public bool Enabled { get; set; } = true;

    public async Task<T> RunAsync<T>(FleetLocks locks, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var names = locks.DriverIds.Select(i => "driver:" + i)
            .Concat(locks.VehicleIds.Select(i => "vehicle:" + i))
            .Concat(locks.MemberUserIds.Select(i => "member:" + i))
            .Select(n => _scope + n).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var held = new List<SemaphoreSlim>();
        try
        {
            if (Enabled)
            {
                foreach (var name in names)
                {
                    var gate = Locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
                    await gate.WaitAsync(cancellationToken);
                    held.Add(gate);
                }
            }

            return await work(cancellationToken);
        }
        finally
        {
            foreach (var gate in held)
            {
                gate.Release();
            }
        }
    }
}

/// <summary>Company A (admin, dispatcher, driver user), company B (dispatcher), plus an Accepted shipment of A.</summary>
internal sealed class DispatchWorld
{
    public World W { get; } = new();
    public FakeCompanyStore CompanyStore { get; } = new();
    public FakeFleetStore Fleet { get; } = new();
    public FakeUserDirectory Directory { get; } = new();
    public FakeMembershipRoles RoleGrants { get; } = new();
    public FakeFleetTransaction Tx { get; } = new();

    public Guid AdminA { get; } = Guid.NewGuid();
    public Guid DriverUser { get; } = Guid.NewGuid();
    public TransportCompany A => W.CompanyA;
    public TransportCompany B => W.CompanyB;
    public Driver Driver { get; private set; } = null!;
    public Vehicle Truck { get; private set; } = null!;

    public DateTime Now => W.Time.Now;

    public DispatchWorld()
    {
        A.AddMember(AdminA, CompanyRole.Admin, Now);
        W.Companies.Set(AdminA, A.Id, CompanyRole.Admin);
        A.AddMember(W.DispatcherA, CompanyRole.Dispatcher, Now);
        A.AddMember(DriverUser, CompanyRole.Driver, Now);
        W.Companies.Set(DriverUser, A.Id, CompanyRole.Driver);
        B.AddMember(W.DispatcherB, CompanyRole.Dispatcher, Now);
        CompanyStore.Companies[A.Id] = A;
        CompanyStore.Companies[B.Id] = B;
        Driver = RegisterDriver(A, DriverUser, "Dan Driver", "LIC-1");
        Truck = AddVehicle(A, "TRK 1", 5000m, 50m);
    }

    public void AsAdminA() => W.User.Set(AdminA, Roles.CompanyAdmin);

    public Driver RegisterDriver(TransportCompany company, Guid userId, string name, string license)
    {
        if (!company.HasActiveMember(userId, CompanyRole.Driver))
        {
            company.AddMember(userId, CompanyRole.Driver, Now);
        }

        var d = company.RegisterDriver(userId, name, license);
        Fleet.Drivers.Add(d);
        return d;
    }

    public Vehicle AddVehicle(TransportCompany company, string plate, decimal kg, decimal m3)
    {
        var v = company.RegisterVehicle(plate, VehicleType.Truck, Capacity.Of(kg, m3));
        Fleet.Vehicles.Add(v);
        return v;
    }

    /// <summary>A shipment (500 kg / 5 m3) accepted by company A.</summary>
    public Shipment AcceptedShipment()
    {
        var shipment = W.AddShipment(openForQuoting: true);
        var quote = W.AddQuote(shipment, A);
        shipment.AcceptQuote(W.Customer, quote.Id, Now);
        return shipment;
    }

    public TimeWindow Window(int startHours = 24, int hours = 4) => TimeWindow.Of(Now.AddHours(startHours), Now.AddHours(startHours + hours));

    public AssignShipmentCommand Cmd(Driver d, Vehicle v, TimeWindow w) => new(d.Id, v.Id, w.StartUtc, w.EndUtc);

    public ViewerResolver Viewers => W.Viewers;

    public AssignShipmentHandler Assign() =>
        new(new AssignShipmentCommandValidator(), Viewers, W.Store, Fleet, W.Assignments, Tx, W.Uow, W.Time);

    public UnassignShipmentHandler Unassign() =>
        new(new UnassignShipmentCommandValidator(), Viewers, W.Store, W.Assignments, W.Uow, W.Time);

    public GetAvailableVehiclesHandler Vehicles() =>
        new(new AvailabilityQueryValidator(), Viewers, W.Store, Fleet, W.Assignments);

    public GetAvailableDriversHandler Drivers() =>
        new(new AvailabilityQueryValidator(), Viewers, W.Store, Fleet, W.Assignments);

    public CreateCompanyHandler CreateCompany() =>
        new(new CreateCompanyCommandValidator(), W.User, Directory, W.Companies, CompanyStore, RoleGrants, W.Uow, W.Time);

    public AddMemberHandler AddMember() =>
        new(new AddMemberCommandValidator(), Viewers, Directory, W.Companies, CompanyStore, RoleGrants, W.Uow, W.Time);

    public RemoveMemberHandler RemoveMember() =>
        new(Viewers, CompanyStore, Fleet, W.Assignments, RoleGrants, Tx, W.Uow, W.Time);

    public RegisterDriverHandler RegisterDriverHandler() =>
        new(new RegisterDriverCommandValidator(), Viewers, CompanyStore, Fleet, Tx, W.Uow);

    public DriverActivationHandler DriverActivation() =>
        new(Viewers, CompanyStore, Fleet, W.Assignments, Tx, W.Uow);

    public RegisterVehicleHandler RegisterVehicle() =>
        new(new RegisterVehicleCommandValidator(), Viewers, CompanyStore, Fleet, W.Uow);

    public VehicleManagementHandler VehicleManagement() =>
        new(new UpdateVehicleCapacityCommandValidator(), Viewers, Fleet, W.Assignments, Tx, W.Uow);

    public GetDriverHandler GetDriver() => new(Viewers, Fleet);
}
