using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipFlow.Domain.CargoAnalysis;

namespace ShipFlow.Infrastructure.Persistence.Configurations;

public sealed class CargoAnalysisSessionConfiguration : IEntityTypeConfiguration<CargoAnalysisSession>
{
    public void Configure(EntityTypeBuilder<CargoAnalysisSession> builder)
    {
        builder.ToTable("CargoAnalysisSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.CustomerId).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.Property(s => s.TotalWeightKg).HasPrecision(12, 3).IsRequired();
        builder.Property(s => s.TotalOriginalVolumeM3).HasPrecision(12, 3).IsRequired();
        builder.Property(s => s.TotalPackedVolumeM3).HasPrecision(12, 3).IsRequired();

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.CustomerId).HasDatabaseName("IX_CargoAnalysisSessions_CustomerId");
    }
}

public sealed class CargoItemConfiguration : IEntityTypeConfiguration<CargoItem>
{
    public void Configure(EntityTypeBuilder<CargoItem> builder)
    {
        builder.ToTable("CargoItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.SessionId).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(CargoItem.MaxNameLength).IsRequired();
        builder.Property(i => i.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();

        builder.Property(i => i.LengthCm).HasPrecision(8, 2).IsRequired();
        builder.Property(i => i.WidthCm).HasPrecision(8, 2).IsRequired();
        builder.Property(i => i.HeightCm).HasPrecision(8, 2).IsRequired();
        builder.Property(i => i.WeightKg).HasPrecision(10, 2).IsRequired();
        builder.Property(i => i.OriginalVolumeM3).HasPrecision(12, 4).IsRequired();

        builder.Property(i => i.CanBeDisassembled).IsRequired();
        builder.Property(i => i.DisassemblyNotes).HasMaxLength(CargoItem.MaxNotesLength);

        builder.Property(i => i.PackedLengthCm).HasPrecision(8, 2).IsRequired();
        builder.Property(i => i.PackedWidthCm).HasPrecision(8, 2).IsRequired();
        builder.Property(i => i.PackedHeightCm).HasPrecision(8, 2).IsRequired();
        builder.Property(i => i.PackedWeightKg).HasPrecision(10, 2).IsRequired();
        builder.Property(i => i.PackedVolumeM3).HasPrecision(12, 4).IsRequired();

        builder.Property(i => i.PackingComplexity).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(i => i.Fragility).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(i => i.SpecialHandling).HasMaxLength(CargoItem.MaxNotesLength);
        builder.Property(i => i.ConfidenceScore).HasPrecision(5, 2).IsRequired();
        builder.Property(i => i.EstimationNotes).HasMaxLength(CargoItem.MaxNotesLength);

        builder.HasIndex(i => i.SessionId).HasDatabaseName("IX_CargoItems_SessionId");
    }
}
