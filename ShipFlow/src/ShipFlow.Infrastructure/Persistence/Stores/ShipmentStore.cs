using Microsoft.EntityFrameworkCore;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Infrastructure.Persistence.Stores;

public sealed class ShipmentStore : IShipmentStore
{
    private readonly AppDbContext _db;

    public ShipmentStore(AppDbContext db) => _db = db;

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken) =>
        await _db.Shipments.AddAsync(shipment, cancellationToken);

    public Task<Shipment?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Shipments
            .Include(s => s.Stops)
            .Include(s => s.Quotes)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Shipment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Shipments
            .AsNoTracking()
            .Include(s => s.Stops)
            .Include(s => s.Quotes)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<PagedResult<ShipmentSummaryDto>> ListAsync(ShipmentListFilter filter, CancellationToken cancellationToken)
    {
        var query = _db.Shipments.AsNoTracking().AsQueryable();

        if (filter.CustomerId is { } customerId)
        {
            query = query.Where(s => s.CustomerId == customerId);
        }

        if (filter.CarrierCompanyId is { } carrierId)
        {
            query = query.Where(s => s.CarrierCompanyId == carrierId);
        }

        if (filter.OpenForQuotingOnly)
        {
            query = query.Where(s => s.Status == ShipmentStatus.Quoting);
        }

        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(s => statuses.Contains(s.Status));
        }

        if (filter.CreatedFromUtc is { } from)
        {
            query = query.Where(s => s.CreatedAtUtc >= from);
        }

        if (filter.CreatedToUtc is { } to)
        {
            query = query.Where(s => s.CreatedAtUtc <= to);
        }

        if (filter.Search is { } search)
        {
            query = query.Where(s => (s.Reference != null && s.Reference.Contains(search)) || s.Cargo.Description.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .ThenBy(s => s.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new ShipmentSummaryDto(
                s.Id,
                s.Reference,
                s.Status,
                s.CreatedAtUtc,
                s.UpdatedAtUtc,
                s.Cargo.Description,
                s.Cargo.WeightKg,
                s.Cargo.VolumeM3,
                s.Stops.OrderBy(x => x.Sequence).Select(x => x.Address).First(),
                s.Stops.OrderByDescending(x => x.Sequence).Select(x => x.Address).First(),
                s.AgreedPrice != null ? s.AgreedPrice.Amount : (decimal?)null,
                s.AgreedPrice != null ? s.AgreedPrice.Currency : null,
                s.CarrierCompanyId))
            .ToListAsync(cancellationToken);

        return new PagedResult<ShipmentSummaryDto>(items, filter.Page, filter.PageSize, total);
    }
}
