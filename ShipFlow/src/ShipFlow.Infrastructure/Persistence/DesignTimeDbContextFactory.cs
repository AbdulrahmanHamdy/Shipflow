using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create the context without starting the API. Creating a migration does not connect to
/// the database; <c>database update</c> reads the connection string from SHIPFLOW_DESIGNTIME_CONNECTION.
/// No credentials are stored here.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ConnectionVariable = "SHIPFLOW_DESIGNTIME_CONNECTION";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = "Server=localhost,1433;Database=ShipFlow_DesignTime;Trusted_Connection=False;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}
