using Microsoft.Data.SqlClient;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>SQL Server error numbers the persistence layer reacts to, and a helper to find them inside wrapped exceptions.</summary>
internal static class SqlErrors
{
    public const int Deadlock = 1205;
    public const int LockTimeout = 1222;
    public const int UniqueIndexViolation = 2601;
    public const int UniqueConstraintViolation = 2627;

    /// <summary>The first <see cref="SqlException"/> in the exception chain, if any.</summary>
    public static SqlException? Find(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql;
            }
        }

        return null;
    }

    public static bool IsLockFailure(Exception exception) =>
        Find(exception) is { Number: Deadlock or LockTimeout };
}
