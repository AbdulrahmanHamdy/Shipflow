using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>
/// Serializes "check, then write" on drivers, vehicles and memberships. One database transaction takes exclusive,
/// transaction-scoped application locks (<c>sp_getapplock</c>), runs the caller's work, and commits; the locks are released
/// by the commit or rollback.
/// <para>
/// Why this and not an index: an overlap of time windows cannot be expressed as a unique index. Why not SERIALIZABLE: range
/// locks depend on the optimizer picking the intended index and make both competitors lock first and then deadlock.
/// Named locks are independent of query plans and of the isolation level, work for rows that do not exist yet, take a
/// timeout, and make the loser simply wait and then see the winner's committed row.
/// </para>
/// <para>
/// Limits (docs/ARCHITECTURE.md): the guarantee holds only for writers that use this protocol; the database does not
/// reject an overlapping row inserted by other means. Locks are per SQL Server instance/database; they do not span a
/// multi-node database.
/// </para>
/// </summary>
public sealed class FleetTransaction : IFleetTransaction
{
    /// <summary>How long a request waits for a competing booking before it answers 409 (milliseconds).</summary>
    public const int LockTimeoutMilliseconds = 10_000;

    private readonly AppDbContext _db;

    public FleetTransaction(AppDbContext db) => _db = db;

    public async Task<T> RunAsync<T>(FleetLocks locks, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locks);
        ArgumentNullException.ThrowIfNull(work);

        var resources = ResourceNames(locks);
        T result = default!;

        // Execution-strategy pattern: with retry-on-failure enabled the whole unit (transaction, locks, work) runs again
        // from scratch, so nothing tracked by an earlier attempt may survive. With the default non-retrying strategy
        // this runs exactly once.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var resource in resources)
                {
                    await AcquireAsync(transaction, resource, cancellationToken);
                }

                result = await work(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex) when (SqlErrors.IsLockFailure(ex))
            {
                // Deadlock victim or lock timeout: the work did not happen, the client may retry.
                throw new ConcurrencyConflictException();
            }
        });

        return result;
    }

    /// <summary>
    /// All resource names, lower-case and sorted ordinally. Every caller acquires in this one global order
    /// (drivers, then members, then vehicles by name), so two requests can never wait on each other in a cycle.
    /// </summary>
    internal static IReadOnlyList<string> ResourceNames(FleetLocks locks) =>
        locks.DriverIds.Select(id => Name("driver", id))
            .Concat(locks.VehicleIds.Select(id => Name("vehicle", id)))
            .Concat(locks.MemberUserIds.Select(id => Name("member", id)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static string Name(string kind, Guid id) => $"shipflow:{kind}:{id:D}";

    private async Task AcquireAsync(IDbContextTransaction transaction, string resource, CancellationToken cancellationToken)
    {
        DbConnection connection = _db.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add(Parameter(command, "@Resource", DbType.String, resource));
        command.Parameters.Add(Parameter(command, "@LockMode", DbType.String, "Exclusive"));
        command.Parameters.Add(Parameter(command, "@LockOwner", DbType.String, "Transaction"));
        command.Parameters.Add(Parameter(command, "@LockTimeout", DbType.Int32, LockTimeoutMilliseconds));

        DbParameter returnValue = command.CreateParameter();
        returnValue.ParameterName = "@ReturnCode";
        returnValue.DbType = DbType.Int32;
        returnValue.Direction = ParameterDirection.ReturnValue;
        command.Parameters.Add(returnValue);

        await command.ExecuteNonQueryAsync(cancellationToken);

        // sp_getapplock: 0 granted, 1 granted after waiting, -1 timeout, -2 cancelled, -3 deadlock victim, -999 bad call.
        var code = Convert.ToInt32(returnValue.Value, System.Globalization.CultureInfo.InvariantCulture);
        if (code >= 0)
        {
            return;
        }

        if (code is -1 or -3)
        {
            throw new ConcurrencyConflictException();
        }

        throw new InvalidOperationException($"sp_getapplock failed for '{resource}' with code {code}.");
    }

    private static DbParameter Parameter(DbCommand command, string name, DbType type, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        return parameter;
    }
}
