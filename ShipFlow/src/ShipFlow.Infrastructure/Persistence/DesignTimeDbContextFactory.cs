using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;
using Microsoft.Extensions.Configuration;

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
        // Prefer the explicit design‑time env var if the developer set one.
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connection))
        {
            // Fallback to the same development connection string used at runtime.
            // This keeps design‑time operations (e.g., dotnet ef database update) against the real
            // development database instead of a placeholder "ShipFlow_DesignTime" database.
            var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
            var config = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(basePath, "appsettings.json"), optional: true)
                .AddJsonFile(Path.Combine(basePath, "appsettings.Development.json"), optional: true)
                .Build();
            connection = config.GetConnectionString("DefaultConnection") ??
                "Server=localhost;Database=ShipFlow_Dev;Trusted_Connection=True;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}