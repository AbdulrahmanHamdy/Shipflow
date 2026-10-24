using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Infrastructure.Identity;

namespace ShipFlow.Infrastructure.Persistence.Configurations;

public sealed class TransportCompanyConfiguration : IEntityTypeConfiguration<TransportCompany>
{
    public void Configure(EntityTypeBuilder<TransportCompany> builder)
    {
        builder.ToTable("TransportCompanies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(TransportCompany.MaxNameLength).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        builder.HasMany(c => c.Memberships).WithOne().HasForeignKey(m => m.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Memberships).HasField("_memberships").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Name).HasDatabaseName("IX_TransportCompanies_Name");
    }
}

public sealed class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("CompanyMemberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.JoinedAtUtc).IsRequired();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

        // A user can be an active member of a company only once (history rows with IsActive = 0 are allowed).
        builder.HasIndex(m => new { m.CompanyId, m.UserId })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_CompanyMemberships_Company_User_Active");
        builder.HasIndex(m => new { m.UserId, m.IsActive }).HasDatabaseName("IX_CompanyMemberships_User_Active");

        // At most one active membership per user across ALL companies (D-024): the Viewer, the token roles and the
        // "one company per user" rules all rely on it. This is the race backstop for the handlers' pre-check.
        builder.HasIndex(m => m.UserId)
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_CompanyMemberships_User_Active");
    }
}

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.FullName).HasMaxLength(Driver.MaxNameLength).IsRequired();
        builder.Property(d => d.LicenseNumber).HasMaxLength(Driver.MaxLicenseLength).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();

        builder.HasOne<TransportCompany>().WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.CompanyId, d.UserId }).IsUnique().HasDatabaseName("UX_Drivers_Company_User");
        builder.HasIndex(d => new { d.CompanyId, d.LicenseNumber }).IsUnique().HasDatabaseName("UX_Drivers_Company_License");
        builder.HasIndex(d => d.UserId).HasDatabaseName("IX_Drivers_User");
    }
}

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.PlateNumber).HasMaxLength(Vehicle.MaxPlateLength).IsRequired();
        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.IsActive).IsRequired();

        builder.OwnsOne(v => v.Capacity, capacity =>
        {
            capacity.Property(c => c.MaxWeightKg).HasColumnName("CapacityMaxWeightKg").HasPrecision(12, 3);
            capacity.Property(c => c.MaxVolumeM3).HasColumnName("CapacityMaxVolumeM3").HasPrecision(12, 3);
        });
        builder.Navigation(v => v.Capacity).IsRequired();

        builder.HasOne<TransportCompany>().WithMany().HasForeignKey(v => v.CompanyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.CompanyId, v.PlateNumber }).IsUnique().HasDatabaseName("UX_Vehicles_Company_Plate");
        builder.HasIndex(v => new { v.CompanyId, v.IsActive }).HasDatabaseName("IX_Vehicles_Company_Active");
    }
}

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ReleaseReason).HasMaxLength(Assignment.MaxReasonLength);
        builder.Property(a => a.CreatedAtUtc).IsRequired();

        builder.OwnsOne(a => a.Window, window =>
        {
            window.Property(w => w.StartUtc).HasColumnName("WindowStartUtc");
            window.Property(w => w.EndUtc).HasColumnName("WindowEndUtc");
        });
        builder.Navigation(a => a.Window).IsRequired();

        builder.HasOne<Shipment>().WithMany().HasForeignKey(a => a.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TransportCompany>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Driver>().WithMany().HasForeignKey(a => a.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);

        // One live assignment per shipment. Overlapping driver/vehicle windows cannot be expressed as a plain unique index
        // in SQL Server; they are prevented by FleetTransaction (named exclusive locks + re-check), see D-025.
        builder.HasIndex(a => a.ShipmentId)
            .IsUnique()
            .HasFilter("[Status] = 'Active'")
            .HasDatabaseName("UX_Assignments_Shipment_Active");
        builder.HasIndex(a => new { a.DriverId, a.Status }).HasDatabaseName("IX_Assignments_Driver_Status");
        builder.HasIndex(a => new { a.VehicleId, a.Status }).HasDatabaseName("IX_Assignments_Vehicle_Status");
        builder.HasIndex(a => new { a.CompanyId, a.Status }).HasDatabaseName("IX_Assignments_Company_Status");
    }
}
