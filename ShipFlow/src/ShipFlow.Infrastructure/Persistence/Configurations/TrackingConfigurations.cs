using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;
using ShipFlow.Infrastructure.Identity;

namespace ShipFlow.Infrastructure.Persistence.Configurations;

public sealed class TrackingPointConfiguration : IEntityTypeConfiguration<TrackingPoint>
{
    public void Configure(EntityTypeBuilder<TrackingPoint> builder)
    {
        builder.ToTable("TrackingPoints");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.RecordedAtUtc).IsRequired();
        builder.Property(t => t.SpeedKph).HasPrecision(6, 2);

        builder.OwnsOne(t => t.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");
        });
        builder.Navigation(t => t.Location).IsRequired();

        builder.HasOne<Shipment>().WithMany().HasForeignKey(t => t.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.ShipmentId, t.RecordedAtUtc }).HasDatabaseName("IX_TrackingPoints_Shipment_Recorded");
    }
}

public sealed class DeliveryProofConfiguration : IEntityTypeConfiguration<DeliveryProof>
{
    public void Configure(EntityTypeBuilder<DeliveryProof> builder)
    {
        builder.ToTable("DeliveryProofs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.RecipientName).HasMaxLength(DeliveryProof.MaxRecipientLength).IsRequired();
        builder.Property(p => p.DeliveredAtUtc).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(DeliveryProof.MaxNotesLength);
        builder.Property(p => p.SignatureReference).HasMaxLength(DeliveryProof.MaxReferenceLength);
        builder.Property(p => p.PhotoReference).HasMaxLength(DeliveryProof.MaxReferenceLength);

        builder.OwnsOne(p => p.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");
        });

        builder.HasOne<Shipment>().WithMany().HasForeignKey(p => p.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.CapturedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.ShipmentId).HasDatabaseName("IX_DeliveryProofs_Shipment");
    }
}

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(ApplicationUser.MaxFullNameLength).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAtUtc).IsRequired();

        // Identity's own "EmailIndex" is non-unique (uniqueness is enforced in UserManager). Make the database
        // enforce it too, so two concurrent registrations of one address cannot both succeed.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
    }
}
