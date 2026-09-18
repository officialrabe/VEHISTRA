using Fuhrpark.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fuhrpark.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.Property(v => v.InternalNumber).HasMaxLength(32).IsRequired();
        builder.Property(v => v.LicensePlate).HasMaxLength(16);
        builder.Property(v => v.Vin).HasMaxLength(24);
        builder.Property(v => v.Manufacturer).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Variant).HasMaxLength(64);
        builder.Property(v => v.Color).HasMaxLength(32);
        builder.Property(v => v.Hsn).HasMaxLength(8);
        builder.Property(v => v.Tsn).HasMaxLength(8);
        builder.Property(v => v.LeasingCompany).HasMaxLength(128);
        builder.Property(v => v.LeasingContractNumber).HasMaxLength(64);
        builder.Property(v => v.Comment).HasMaxLength(2048);

        builder.HasIndex(v => v.InternalNumber).IsUnique().HasDatabaseName("IX_Vehicles_InternalNumber");
        builder.HasIndex(v => v.LicensePlate).HasDatabaseName("IX_Vehicles_LicensePlate");
        builder.HasIndex(v => v.Vin).HasDatabaseName("IX_Vehicles_Vin");
        builder.HasIndex(v => v.VehicleStatusId);
        builder.HasIndex(v => v.CurrentDriverId);
        builder.HasIndex(v => v.NextInspectionDue);
        builder.HasIndex(v => new { v.IsRetired, v.IsRegistered });

        builder.HasOne(v => v.Status)
            .WithMany()
            .HasForeignKey(v => v.VehicleStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CurrentDriver)
            .WithMany()
            .HasForeignKey(v => v.CurrentDriverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.Retirement)
            .WithOne(r => r.Vehicle!)
            .HasForeignKey<VehicleRetirement>(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VehicleCategoryConfiguration : IEntityTypeConfiguration<VehicleCategory>
{
    public void Configure(EntityTypeBuilder<VehicleCategory> builder)
    {
        builder.ToTable("VehicleCategories");

        builder.Property(c => c.Name).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(512);
        builder.Property(c => c.ColorHex).HasMaxLength(9);

        builder.HasIndex(c => c.Name).IsUnique();
    }
}

public sealed class VehicleCategoryAssignmentConfiguration : IEntityTypeConfiguration<VehicleCategoryAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleCategoryAssignment> builder)
    {
        builder.ToTable("VehicleCategoryAssignments");

        builder.HasIndex(a => new { a.VehicleId, a.VehicleCategoryId }).IsUnique();

        builder.HasOne(a => a.Vehicle)
            .WithMany(v => v.CategoryAssignments)
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Category)
            .WithMany(c => c.Assignments)
            .HasForeignKey(a => a.VehicleCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VehicleStatusConfiguration : IEntityTypeConfiguration<VehicleStatus>
{
    public void Configure(EntityTypeBuilder<VehicleStatus> builder)
    {
        builder.ToTable("VehicleStatuses");

        builder.Property(s => s.Name).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(512);
        builder.Property(s => s.ColorHex).HasMaxLength(9);

        builder.HasIndex(s => s.Name).IsUnique();
        builder.HasIndex(s => s.Kind);
    }
}

public sealed class VehicleStatusHistoryConfiguration : IEntityTypeConfiguration<VehicleStatusHistory>
{
    public void Configure(EntityTypeBuilder<VehicleStatusHistory> builder)
    {
        builder.ToTable("VehicleStatusHistory");

        builder.Property(h => h.Reason).HasMaxLength(256);
        builder.Property(h => h.Comment).HasMaxLength(1024);
        builder.Property(h => h.ChangedByUserName).HasMaxLength(128);

        builder.HasIndex(h => new { h.VehicleId, h.ChangedAt });

        builder.HasOne(h => h.Vehicle)
            .WithMany(v => v.StatusHistory)
            .HasForeignKey(h => h.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.OldStatus)
            .WithMany()
            .HasForeignKey(h => h.OldStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.NewStatus)
            .WithMany()
            .HasForeignKey(h => h.NewStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");

        builder.Property(d => d.PersonnelNumber).HasMaxLength(32);
        builder.Property(d => d.FirstName).HasMaxLength(64);
        builder.Property(d => d.LastName).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Phone).HasMaxLength(32);
        builder.Property(d => d.Mobile).HasMaxLength(32);
        builder.Property(d => d.Email).HasMaxLength(256);
        builder.Property(d => d.Comment).HasMaxLength(2048);

        builder.HasIndex(d => d.PersonnelNumber).IsUnique().HasFilter(null);
        builder.HasIndex(d => new { d.LastName, d.FirstName });
    }
}

public sealed class VehicleDriverAssignmentConfiguration : IEntityTypeConfiguration<VehicleDriverAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleDriverAssignment> builder)
    {
        builder.ToTable("VehicleDriverAssignments");

        builder.Property(a => a.Comment).HasMaxLength(1024);
        builder.Property(a => a.AssignedByUserName).HasMaxLength(128);

        builder.HasIndex(a => new { a.VehicleId, a.ValidFrom });
        builder.HasIndex(a => new { a.DriverId, a.ValidFrom });
        builder.HasIndex(a => a.ValidTo);

        builder.HasOne(a => a.Vehicle)
            .WithMany(v => v.DriverAssignments)
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Driver)
            .WithMany(d => d.VehicleAssignments)
            .HasForeignKey(a => a.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MileageEntryConfiguration : IEntityTypeConfiguration<MileageEntry>
{
    public void Configure(EntityTypeBuilder<MileageEntry> builder)
    {
        builder.ToTable("MileageEntries");

        builder.Property(m => m.Comment).HasMaxLength(512);
        builder.Property(m => m.RecordedByUserName).HasMaxLength(128);

        builder.HasIndex(m => new { m.VehicleId, m.RecordedAt }).HasDatabaseName("IX_MileageEntries_Vehicle_RecordedAt");

        builder.HasOne(m => m.Vehicle)
            .WithMany(v => v.MileageEntries)
            .HasForeignKey(m => m.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
