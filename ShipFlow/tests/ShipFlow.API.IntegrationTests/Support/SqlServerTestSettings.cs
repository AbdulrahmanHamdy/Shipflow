using System.Data.Common;

namespace ShipFlow.API.IntegrationTests.Support;

/// <summary>
/// Tests that exercise SQL Server behavior (constraints, concurrency tokens, Identity stores) need a real server;
/// the EF in-memory provider cannot tell us anything true about those. Set SHIPFLOW_TEST_SQLSERVER to a connection
/// string for a server where the login may create and drop databases. Each test class gets its own throwaway database.
/// Without it those tests are reported as skipped, never as passed.
/// </summary>
public static class SqlServerTestSettings
{
    public const string Variable = "SHIPFLOW_TEST_SQLSERVER";

    public static string? ServerConnectionString => Environment.GetEnvironmentVariable(Variable);

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ServerConnectionString);

    public static string ForDatabase(string databaseName)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = ServerConnectionString };
        builder["Initial Catalog"] = databaseName;
        return builder.ConnectionString;
    }

    public static string MasterConnection() => ForDatabase("master");
}

/// <summary>[Fact] that is skipped (visibly) unless a SQL Server is available. See <see cref="SqlServerTestSettings"/>.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerTestSettings.IsConfigured)
        {
            Skip = $"Set {SqlServerTestSettings.Variable} to run SQL Server tests.";
        }
    }
}
