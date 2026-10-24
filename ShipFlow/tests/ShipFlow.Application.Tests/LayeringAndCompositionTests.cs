using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipFlow.Application;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Features.Auth.Login;
using ShipFlow.Application.Features.Auth.Register;
using ShipFlow.Infrastructure;

namespace ShipFlow.Application.Tests;

/// <summary>
/// Project-reference layering rules live in ShipFlow.Domain.Tests.DomainArchitectureTests (they read the
/// .csproj files). This class checks that the layers compose into a valid service provider.
/// </summary>
public class CompositionTests
{
    [Fact]
    public void AddApplicationAndInfrastructure_BuildAValidServiceProvider()
    {
        // The empty host builder supplies what the real host provides: IConfiguration, IHostEnvironment, logging.
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing",
            Configuration = new ConfigurationManager()
        });
        // Resolving the DbContext needs a connection string; nothing connects to it in this test.
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ShipFlow_CompositionTest;TrustServerCertificate=True";
        builder.Services.AddLogging();
        builder.Services.AddScoped<ICurrentUser, ShipFlow.Application.Tests.Shipments.FakeCurrentUser>();

        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);

        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        Assert.NotNull(provider.GetRequiredService<TimeProvider>());
        Assert.NotNull(provider.GetRequiredService<ITokenService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdentityService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RegisterHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LoginHandler>());
        // Phase 4: the handlers that need the locking transaction, role staging and fleet ports.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ShipFlow.Application.Features.Dispatching.AssignShipmentHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ShipFlow.Application.Features.Companies.RemoveMemberHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ShipFlow.Application.Features.Companies.CreateCompanyHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ShipFlow.Application.Features.Fleet.DriverActivationHandler>());
    }
}
