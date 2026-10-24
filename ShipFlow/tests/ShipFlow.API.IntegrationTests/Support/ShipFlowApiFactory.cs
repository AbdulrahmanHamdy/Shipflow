using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ShipFlow.API.IntegrationTests.Support;

/// <summary>
/// Boots the real API with test configuration. Nothing secret is committed: the JWT signing key is random per
/// test process, and the database is whatever <see cref="SqlServerTestSettings"/> provides (or an unused placeholder
/// for tests that never reach the database). Per-test settings go through <see cref="FactoryExtensions.WithSettings"/>.
/// </summary>
public sealed class ShipFlowApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "ShipFlow.Tests";
    public const string Audience = "ShipFlow.Tests.Clients";

    public static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = SigningKey,
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ShipFlow_Unused;TrustServerCertificate=True",
                ["Database:MigrateOnStartup"] = "false",
                ["Database:SeedReferenceData"] = "false",
                ["Database:SeedDevelopmentData"] = "false",
                ["RateLimiting:AuthPermitLimit"] = "1000"
            };

            configuration.AddInMemoryCollection(settings);
        });
    }
}

public static class FactoryExtensions
{
    /// <summary>A derived factory whose configuration also contains <paramref name="settings"/> (later sources win).</summary>
    public static WebApplicationFactory<Program> WithSettings(
        this WebApplicationFactory<Program> factory,
        IDictionary<string, string?> settings,
        string? environment = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (environment is not null)
            {
                builder.UseEnvironment(environment);
            }

            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        });
}
