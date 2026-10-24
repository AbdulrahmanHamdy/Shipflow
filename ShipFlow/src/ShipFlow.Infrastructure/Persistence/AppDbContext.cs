using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;
using ShipFlow.Infrastructure.Identity;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context: ASP.NET Core Identity tables plus the domain aggregates. Mapping lives in
/// <c>Persistence/Configurations</c>; the domain types carry no ORM attributes.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<ShipmentStop> ShipmentStops => Set<ShipmentStop>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();

    public DbSet<TransportCompany> TransportCompanies => Set<TransportCompany>();

    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<TrackingPoint> TrackingPoints => Set<TrackingPoint>();

    public DbSet<DeliveryProof> DeliveryProofs => Set<DeliveryProof>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // All persisted instants are UTC; see UtcDateTimeConverter.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}
