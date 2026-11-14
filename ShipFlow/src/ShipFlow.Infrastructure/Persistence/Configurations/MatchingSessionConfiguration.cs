using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipFlow.Domain.Dispatching;

namespace ShipFlow.Infrastructure.Persistence.Configurations;

public sealed class MatchingSessionConfiguration : IEntityTypeConfiguration<MatchingSession>
{
    public void Configure(EntityTypeBuilder<MatchingSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartedAtUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.Status).HasDefaultValue(MatchingSessionStatus.Active);
        builder.HasOne(x => x.Shipment)
               .WithOne()
               .HasForeignKey<MatchingSession>(x => x.ShipmentId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
