using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;
using ShipFlow.Infrastructure.Identity;

namespace ShipFlow.Infrastructure.Persistence.Configurations;

// Mapping rules used throughout (see docs/ARCHITECTURE.md "Persistence mapping"):
//  * Keys are client-generated Guids -> ValueGeneratedNever, so EF treats entities added to a tracked aggregate as new rows.
//  * Enums are stored as strings (readable, safe against reordering) with an explicit max length.
//  * Value objects are owned types mapped into the owner's table with explicit column names.
//  * Foreign keys to users and across aggregates are Restrict; only aggregate children cascade.

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Reference).HasMaxLength(Shipment.MaxReferenceLength);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        // Optimistic concurrency: two concurrent writers of the same shipment cannot both win (e.g. two quote
        // acceptances). SQL Server bumps rowversion on every UPDATE of the row. Shadow property: the domain stays ORM-free.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.OwnsOne(s => s.Cargo, cargo =>
        {
            cargo.Property(c => c.Description).HasColumnName("CargoDescription").HasMaxLength(Cargo.MaxDescriptionLength).IsRequired();
            cargo.Property(c => c.WeightKg).HasColumnName("CargoWeightKg").HasPrecision(12, 3);
            cargo.Property(c => c.VolumeM3).HasColumnName("CargoVolumeM3").HasPrecision(12, 3);
        });
        builder.Navigation(s => s.Cargo).IsRequired();

        builder.OwnsOne(s => s.AgreedPrice, price =>
        {
            price.Property(m => m.Amount).HasColumnName("AgreedPriceAmount").HasPrecision(18, 4);
            price.Property(m => m.Currency).HasColumnName("AgreedPriceCurrency").HasMaxLength(3).IsFixedLength();
        });

        builder.HasMany(s => s.Stops).WithOne().HasForeignKey("ShipmentId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Stops).HasField("_stops").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Quotes).WithOne().HasForeignKey(q => q.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Quotes).HasField("_quotes").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Events).WithOne().HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Events).HasField("_events").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TransportCompany>().WithMany().HasForeignKey(s => s.CarrierCompanyId).OnDelete(DeleteBehavior.Restrict);

        // AcceptedQuoteId and CurrentAssignmentId are plain columns on purpose: a foreign key would create a
        // circular dependency with Quotes/Assignments. The aggregate methods keep them consistent.
        builder.HasIndex(s => new { s.CustomerId, s.CreatedAtUtc }).HasDatabaseName("IX_Shipments_Customer_Created");
        builder.HasIndex(s => new { s.Status, s.CreatedAtUtc }).HasDatabaseName("IX_Shipments_Status_Created");
        builder.HasIndex(s => s.CarrierCompanyId).HasDatabaseName("IX_Shipments_Carrier");
    }
}

public sealed class ShipmentStopConfiguration : IEntityTypeConfiguration<ShipmentStop>
{
    public void Configure(EntityTypeBuilder<ShipmentStop> builder)
    {
        builder.ToTable("ShipmentStops");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Sequence).IsRequired();
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(ShipmentStop.MaxAddressLength).IsRequired();

        builder.OwnsOne(s => s.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");
        });
        builder.Navigation(s => s.Location).IsRequired();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.StartUtc).HasColumnName("WindowStartUtc");
            window.Property(w => w.EndUtc).HasColumnName("WindowEndUtc");
        });

        // The domain stop has no ShipmentId property; the foreign key is a shadow property. It is declared here so
        // the index below does not depend on the order in which configurations are applied. The relationship
        // itself is configured from ShipmentConfiguration (HasForeignKey("ShipmentId")).
        builder.Property<Guid>("ShipmentId");
        builder.HasIndex("ShipmentId", nameof(ShipmentStop.Sequence)).IsUnique().HasDatabaseName("UX_ShipmentStops_Shipment_Sequence");
    }
}

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(q => q.Terms).HasMaxLength(Quote.MaxTermsLength).IsRequired();
        builder.Property(q => q.CreatedAtUtc).IsRequired();
        builder.Property(q => q.ExpiresAtUtc).IsRequired();

        builder.OwnsOne(q => q.Price, price =>
        {
            price.Property(m => m.Amount).HasColumnName("PriceAmount").HasPrecision(18, 4);
            price.Property(m => m.Currency).HasColumnName("PriceCurrency").HasMaxLength(3).IsFixedLength();
        });
        builder.Navigation(q => q.Price).IsRequired();

        builder.HasOne<TransportCompany>().WithMany().HasForeignKey(q => q.CompanyId).OnDelete(DeleteBehavior.Restrict);

        // Database-level backstops for domain invariants (the aggregate enforces them first):
        //  * at most one live quote per company per shipment;
        //  * at most one accepted quote per shipment, even if application code were bypassed.
        builder.HasIndex(q => new { q.ShipmentId, q.CompanyId })
            .IsUnique()
            .HasFilter("[Status] = 'Submitted'")
            .HasDatabaseName("UX_Quotes_Shipment_Company_Submitted");
        builder.HasIndex(q => q.ShipmentId)
            .IsUnique()
            .HasFilter("[Status] = 'Accepted'")
            .HasDatabaseName("UX_Quotes_Shipment_Accepted");
        builder.HasIndex(q => new { q.CompanyId, q.Status }).HasDatabaseName("IX_Quotes_Company_Status");
    }
}

public sealed class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    private static readonly ValueConverter<IReadOnlyDictionary<string, string>, string> MetadataConverter =
        new(v => MetadataJson.Serialize(v), v => MetadataJson.Deserialize(v));

    private static readonly ValueComparer<IReadOnlyDictionary<string, string>> MetadataComparer =
        new((a, b) => MetadataJson.AreEqual(a, b), v => MetadataJson.Hash(v), v => v);

    public void Configure(EntityTypeBuilder<ShipmentEvent> builder)
    {
        builder.ToTable("ShipmentEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.OccurredAtUtc).IsRequired();
        // ActorId is a user id or, for carrier actions, a company id, so it has no foreign key.
        builder.Property(e => e.ActorId).IsRequired();
        builder.Property(e => e.PreviousStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.NewStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Metadata)
            .HasConversion(MetadataConverter, MetadataComparer)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.HasIndex(e => new { e.ShipmentId, e.OccurredAtUtc }).HasDatabaseName("IX_ShipmentEvents_Shipment_Occurred");
    }
}
