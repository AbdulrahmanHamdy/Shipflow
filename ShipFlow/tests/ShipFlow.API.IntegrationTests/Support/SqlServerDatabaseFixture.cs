using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShipFlow.Infrastructure.Persistence;

namespace ShipFlow.API.IntegrationTests.Support;

/// <summary>Creates a uniquely named database for a test class and drops it afterwards.</summary>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private readonly string _databaseName = "ShipFlow_Test_" + Guid.NewGuid().ToString("N");

    public string ConnectionString => SqlServerTestSettings.ForDatabase(_databaseName);

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        if (!SqlServerTestSettings.IsConfigured)
        {
            return;
        }

        // No migration has been generated yet, so the schema comes from the model. Once InitialCreate exists, switch
        // to MigrateAsync() so the tests also prove the migration matches the model (docs/TESTING.md).
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (!SqlServerTestSettings.IsConfigured)
        {
            return;
        }

        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(SqlServerTestSettings.MasterConnection());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}
