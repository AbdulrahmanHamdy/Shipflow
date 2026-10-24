using Microsoft.EntityFrameworkCore;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Features.Companies;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Infrastructure.Persistence.Stores;

public sealed class CompanyStore : ICompanyStore
{
    private readonly AppDbContext _db;

    public CompanyStore(AppDbContext db) => _db = db;

    public async Task AddAsync(TransportCompany company, CancellationToken cancellationToken) =>
        await _db.TransportCompanies.AddAsync(company, cancellationToken);

    public Task<TransportCompany?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _db.TransportCompanies
            .Include(c => c.Memberships)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<TransportCompany?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _db.TransportCompanies
            .AsNoTracking()
            .Include(c => c.Memberships)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid companyId, bool includeFormer, CancellationToken cancellationToken)
    {
        // Projected to plain values first: the role is stored as text and is turned into a string on the client.
        var rows = await (
            from m in _db.CompanyMemberships.AsNoTracking()
            join u in _db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.CompanyId == companyId && (includeFormer || m.IsActive)
            orderby m.JoinedAtUtc, m.Id
            select new { m.UserId, u.Email, u.FullName, m.Role, m.IsActive, m.JoinedAtUtc, m.LeftAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new MemberDto(
                r.UserId, r.Email ?? string.Empty, r.FullName, r.Role.ToString(), r.IsActive, r.JoinedAtUtc, r.LeftAtUtc))
            .ToList();
    }
}

public sealed class FleetStore : IFleetStore
{
    private readonly AppDbContext _db;

    public FleetStore(AppDbContext db) => _db = db;

    public async Task AddDriverAsync(Driver driver, CancellationToken cancellationToken) =>
        await _db.Drivers.AddAsync(driver, cancellationToken);

    public async Task AddVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken) =>
        await _db.Vehicles.AddAsync(vehicle, cancellationToken);

    public Task<Driver?> FindDriverAsync(Guid companyId, Guid driverId, CancellationToken cancellationToken) =>
        _db.Drivers.AsNoTracking().SingleOrDefaultAsync(d => d.CompanyId == companyId && d.Id == driverId, cancellationToken);

    public Task<Driver?> FindDriverForUpdateAsync(Guid companyId, Guid driverId, CancellationToken cancellationToken) =>
        _db.Drivers.SingleOrDefaultAsync(d => d.CompanyId == companyId && d.Id == driverId, cancellationToken);

    public Task<Driver?> FindDriverByUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken) =>
        _db.Drivers.AsNoTracking().SingleOrDefaultAsync(d => d.CompanyId == companyId && d.UserId == userId, cancellationToken);

    public Task<Vehicle?> FindVehicleAsync(Guid companyId, Guid vehicleId, CancellationToken cancellationToken) =>
        _db.Vehicles.AsNoTracking().SingleOrDefaultAsync(v => v.CompanyId == companyId && v.Id == vehicleId, cancellationToken);

    public Task<Vehicle?> FindVehicleForUpdateAsync(Guid companyId, Guid vehicleId, CancellationToken cancellationToken) =>
        _db.Vehicles.SingleOrDefaultAsync(v => v.CompanyId == companyId && v.Id == vehicleId, cancellationToken);

    public async Task<PagedResult<Driver>> ListDriversAsync(
        Guid companyId, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Drivers.AsNoTracking().Where(d => d.CompanyId == companyId);
        if (isActive is { } active)
        {
            query = query.Where(d => d.IsActive == active);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(d => d.FullName)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Driver>(items, page, pageSize, total);
    }

    public async Task<PagedResult<Vehicle>> ListVehiclesAsync(
        Guid companyId, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Vehicles.AsNoTracking().Where(v => v.CompanyId == companyId);
        if (isActive is { } active)
        {
            query = query.Where(v => v.IsActive == active);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(v => v.PlateNumber)
            .ThenBy(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Vehicle>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<Driver>> ListActiveDriversAsync(Guid companyId, CancellationToken cancellationToken) =>
        await _db.Drivers.AsNoTracking().Where(d => d.CompanyId == companyId && d.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListActiveVehiclesAsync(Guid companyId, CancellationToken cancellationToken) =>
        await _db.Vehicles.AsNoTracking().Where(v => v.CompanyId == companyId && v.IsActive).ToListAsync(cancellationToken);
}

public sealed class AssignmentStore : IAssignmentStore
{
    private readonly AppDbContext _db;

    public AssignmentStore(AppDbContext db) => _db = db;

    public async Task AddAsync(Assignment assignment, CancellationToken cancellationToken) =>
        await _db.Assignments.AddAsync(assignment, cancellationToken);

    public Task<Assignment?> FindActiveForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        _db.Assignments.SingleOrDefaultAsync(
            a => a.ShipmentId == shipmentId && a.Status == AssignmentStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<Assignment>> ListActiveOverlappingAsync(
        Guid companyId, TimeWindow window, CancellationToken cancellationToken)
    {
        // Half-open overlap: [a.Start, a.End) intersects [start, end) iff a.Start < end && start < a.End.
        var start = window.StartUtc;
        var end = window.EndUtc;
        return await _db.Assignments
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId
                        && a.Status == AssignmentStatus.Active
                        && a.Window.StartUtc < end
                        && a.Window.EndUtc > start)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Assignment>> ListActiveOverlappingForAsync(
        Guid driverId, Guid vehicleId, TimeWindow window, CancellationToken cancellationToken)
    {
        var start = window.StartUtc;
        var end = window.EndUtc;
        return await _db.Assignments
            .AsNoTracking()
            .Where(a => (a.DriverId == driverId || a.VehicleId == vehicleId)
                        && a.Status == AssignmentStatus.Active
                        && a.Window.StartUtc < end
                        && a.Window.EndUtc > start)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveForDriverAsync(Guid driverId, CancellationToken cancellationToken) =>
        _db.Assignments.AnyAsync(a => a.DriverId == driverId && a.Status == AssignmentStatus.Active, cancellationToken);

    public Task<bool> HasActiveForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        _db.Assignments.AnyAsync(a => a.VehicleId == vehicleId && a.Status == AssignmentStatus.Active, cancellationToken);
}
