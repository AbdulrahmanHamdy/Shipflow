using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>Readiness check: can the application reach its SQL Server database?</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public DatabaseHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Details stay out of the response; the health writer never prints them either.
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
