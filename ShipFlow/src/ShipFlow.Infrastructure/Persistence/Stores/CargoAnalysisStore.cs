using Microsoft.EntityFrameworkCore;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Domain.CargoAnalysis;

namespace ShipFlow.Infrastructure.Persistence.Stores;

public sealed class CargoAnalysisStore : ICargoAnalysisStore
{
    private readonly AppDbContext _context;

    public CargoAnalysisStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CargoAnalysisSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _context.CargoAnalysisSessions.AddAsync(session, cancellationToken);
    }

    public async Task<CargoAnalysisSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.CargoAnalysisSessions
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<CargoAnalysisSession?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.CargoAnalysisSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
}
