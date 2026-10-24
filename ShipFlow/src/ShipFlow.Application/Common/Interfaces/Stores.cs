using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Features.Companies;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Common.Interfaces;

// Narrow, use-case shaped persistence ports (no generic repository, no DbContext in Application).

public interface IShipmentStore
{
    Task AddAsync(Shipment shipment, CancellationToken cancellationToken);

    /// <summary>Tracked shipment with stops and quotes, for changes. Null when it does not exist.</summary>
    Task<Shipment?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Untracked shipment with stops and quotes, for reads.</summary>
    Task<Shipment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ShipmentSummaryDto>> ListAsync(ShipmentListFilter filter, CancellationToken cancellationToken);
}

public interface IAssignmentStore
{
    Task AddAsync(Assignment assignment, CancellationToken cancellationToken);

    /// <summary>The tracked active assignment of a shipment, if any (released when a shipment is cancelled).</summary>
    Task<Assignment?> FindActiveForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken);

    /// <summary>Untracked active assignments of the company whose window overlaps <paramref name="window"/> (half-open).</summary>
    Task<IReadOnlyList<Assignment>> ListActiveOverlappingAsync(Guid companyId, TimeWindow window, CancellationToken cancellationToken);

    /// <summary>
    /// Untracked active assignments overlapping <paramref name="window"/> that use the given driver <b>or</b> the given
    /// vehicle: exactly what <c>Assignment.Create</c> needs for its conflict check.
    /// </summary>
    Task<IReadOnlyList<Assignment>> ListActiveOverlappingForAsync(
        Guid driverId, Guid vehicleId, TimeWindow window, CancellationToken cancellationToken);

    /// <summary>Any Active assignment (whatever its window) uses this driver.</summary>
    Task<bool> HasActiveForDriverAsync(Guid driverId, CancellationToken cancellationToken);

    /// <summary>Any Active assignment (whatever its window) uses this vehicle.</summary>
    Task<bool> HasActiveForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken);
}

/// <summary>A user's active membership of an active transport company, resolved from the database.</summary>
public sealed record CompanyMembershipInfo(Guid CompanyId, CompanyRole Role)
{
    public bool CanManageQuotes => Role is CompanyRole.Admin or CompanyRole.Dispatcher;
}

public interface ICompanyAccess
{
    Task<CompanyMembershipInfo?> FindActiveMembershipAsync(Guid userId, CancellationToken cancellationToken);
}

public interface ICompanyStore
{
    Task AddAsync(TransportCompany company, CancellationToken cancellationToken);

    /// <summary>Tracked company with its memberships, for changes. Null when it does not exist.</summary>
    Task<TransportCompany?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Untracked company with its memberships, for reads and checks.</summary>
    Task<TransportCompany?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Members with their account details. Former members are included only when asked for.</summary>
    Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid companyId, bool includeFormer, CancellationToken cancellationToken);
}

/// <summary>
/// Drivers and vehicles. Every lookup is scoped by company id, so another company's resource is simply "not found".
/// </summary>
public interface IFleetStore
{
    Task AddDriverAsync(Driver driver, CancellationToken cancellationToken);

    Task AddVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken);

    Task<Driver?> FindDriverAsync(Guid companyId, Guid driverId, CancellationToken cancellationToken);

    Task<Driver?> FindDriverForUpdateAsync(Guid companyId, Guid driverId, CancellationToken cancellationToken);

    /// <summary>Untracked driver profile of a user of this company (active or not), if one exists.</summary>
    Task<Driver?> FindDriverByUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken);

    Task<Vehicle?> FindVehicleAsync(Guid companyId, Guid vehicleId, CancellationToken cancellationToken);

    Task<Vehicle?> FindVehicleForUpdateAsync(Guid companyId, Guid vehicleId, CancellationToken cancellationToken);

    Task<PagedResult<Driver>> ListDriversAsync(Guid companyId, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);

    Task<PagedResult<Vehicle>> ListVehiclesAsync(Guid companyId, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Driver>> ListActiveDriversAsync(Guid companyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListActiveVehiclesAsync(Guid companyId, CancellationToken cancellationToken);
}

public sealed record UserSummary(Guid Id, string Email, string FullName);

/// <summary>Looks accounts up. Only active accounts are ever returned.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> FindActiveByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserSummary?> FindActiveByEmailAsync(string email, CancellationToken cancellationToken);
}

/// <summary>
/// Keeps the Identity roles (token claims) consistent with company memberships. The changes are only <b>staged</b> on the
/// current unit of work: they are written by <see cref="IUnitOfWork.SaveChangesAsync"/> in the same transaction as the
/// membership change, so a membership and its role never disagree in the database.
/// </summary>
public interface IMembershipRoles
{
    Task StageGrantAsync(Guid userId, CompanyRole role, CancellationToken cancellationToken);

    Task StageRevokeAsync(Guid userId, CompanyRole role, CancellationToken cancellationToken);
}

/// <summary>Named, transaction-scoped exclusive locks. Acquired in a fixed global order by the implementation.</summary>
public sealed record FleetLocks(
    IReadOnlyCollection<Guid> DriverIds,
    IReadOnlyCollection<Guid> VehicleIds,
    IReadOnlyCollection<Guid> MemberUserIds)
{
    public static FleetLocks None { get; } = new([], [], []);

    public static FleetLocks ForDriver(Guid driverId) => new([driverId], [], []);

    public static FleetLocks ForVehicle(Guid vehicleId) => new([], [vehicleId], []);

    public static FleetLocks ForMember(Guid userId) => new([], [], [userId]);
}

/// <summary>
/// Runs <c>work</c> inside one database transaction after taking exclusive locks on the given drivers, vehicles and
/// members. This is what makes "check for conflicts, then insert" safe against concurrent requests (docs/ARCHITECTURE.md,
/// D-025). <c>work</c> must read everything it decides on <b>inside</b> the callback (after the locks are held), and must
/// be safe to run again from scratch (an execution strategy may retry it). A lock that cannot be obtained in time, or a
/// deadlock, surfaces as <see cref="Exceptions.ConcurrencyConflictException"/> (HTTP 409).
/// </summary>
public interface IFleetTransaction
{
    Task<T> RunAsync<T>(FleetLocks locks, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    /// <summary>
    /// Commits all tracked changes in one transaction. Throws <c>ConcurrencyConflictException</c> when another
    /// request changed the same shipment first, and <c>ConflictException</c> when a uniqueness rule was hit (duplicate
    /// plate, licence, membership...).
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
