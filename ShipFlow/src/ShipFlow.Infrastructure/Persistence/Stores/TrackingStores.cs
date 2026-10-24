using Microsoft.EntityFrameworkCore;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Tracking;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;

namespace ShipFlow.Infrastructure.Persistence.Stores;

public sealed class TrackingStore : ITrackingStore
{
    private readonly AppDbContext _db;

    public TrackingStore(AppDbContext db) => _db = db;

    public async Task AddPointAsync(TrackingPoint point, CancellationToken cancellationToken) =>
        await _db.TrackingPoints.AddAsync(point, cancellationToken);

    public Task<TrackingPoint?> FindLatestAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        _db.TrackingPoints
            .AsNoTracking()
            .Where(t => t.ShipmentId == shipmentId)
            .OrderByDescending(t => t.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<TrackingPointDto>> ListAsync(TrackingListFilter filter, CancellationToken cancellationToken)
    {
        var query = _db.TrackingPoints.AsNoTracking().Where(t => t.ShipmentId == filter.ShipmentId);

        if (filter.FromUtc is { } from)
        {
            query = query.Where(t => t.RecordedAtUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(t => t.RecordedAtUtc <= to);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.RecordedAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(t => new TrackingPointDto(
                t.Id,
                t.ShipmentId,
                t.Location.Latitude,
                t.Location.Longitude,
                t.RecordedAtUtc,
                t.SpeedKph))
            .ToListAsync(cancellationToken);

        return new PagedResult<TrackingPointDto>(items, filter.Page, filter.PageSize, total);
    }

    public Task<bool> ExistsAsync(Guid shipmentId, DateTime recordedAtUtc, Guid recordedByUserId, CancellationToken cancellationToken) =>
        _db.TrackingPoints
            .AnyAsync(t => t.ShipmentId == shipmentId && t.RecordedAtUtc == recordedAtUtc && t.RecordedByUserId == recordedByUserId, cancellationToken);

    public async Task AddProofAsync(DeliveryProof proof, CancellationToken cancellationToken) =>
        await _db.DeliveryProofs.AddAsync(proof, cancellationToken);

    public Task<DeliveryProof?> FindProofAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        _db.DeliveryProofs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ShipmentId == shipmentId, cancellationToken);
}

public sealed class DriverAssignmentStore : IDriverAssignmentStore
{
    private readonly AppDbContext _db;

    public DriverAssignmentStore(AppDbContext db) => _db = db;

    public Task<Assignment?> FindCurrentForDriverAsync(Guid driverId, CancellationToken cancellationToken) =>
        _db.Assignments
            .FirstOrDefaultAsync(a => a.DriverId == driverId && a.Status == AssignmentStatus.Active, cancellationToken);

    public async Task<PagedResult<AssignmentDto>> ListForDriverAsync(Guid driverId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = from a in _db.Assignments.AsNoTracking()
                    join s in _db.Shipments.AsNoTracking() on a.ShipmentId equals s.Id
                    where a.DriverId == driverId
                    select new { Assignment = a, ShipmentStatus = s.Status };

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.Assignment.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => x.Assignment.ToDto(x.ShipmentStatus))
            .ToList();

        return new PagedResult<AssignmentDto>(items, page, pageSize, total);
    }
}
