using Fuhrpark.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fuhrpark.Infrastructure.Persistence.Configurations;

public sealed class VehicleInspectionConfiguration : IEntityTypeConfiguration<VehicleInspection>
{
    public void Configure(EntityTypeBuilder<VehicleInspection> builder)
    {
        builder.ToTable("VehicleInspections");

        builder.Property(i => i.TestCenter).HasMaxLength(128);
        builder.Property(i => i.Inspector).HasMaxLength(128);
        builder.Property(i => i.Defects).HasMaxLength(2048);
        builder.Property(i => i.Comment).HasMaxLength(2048);

        builder.HasIndex(i => new { i.VehicleId, i.InspectionDate });
        builder.HasIndex(i => i.NextDueDate);

        builder.HasOne(i => i.Vehicle)
            .WithMany(v => v.Inspections)
            .HasForeignKey(i => i.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Document)
            .WithMany()
            .HasForeignKey(i => i.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MaintenanceRuleConfiguration : IEntityTypeConfiguration<MaintenanceRule>
{
    public void Configure(EntityTypeBuilder<MaintenanceRule> builder)
    {
        builder.ToTable("MaintenanceRules");

        builder.Property(r => r.Name).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1024);

        builder.HasIndex(r => new { r.VehicleId, r.IsActive });

        builder.HasOne(r => r.Vehicle)
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.VehicleCategory)
            .WithMany()
            .HasForeignKey(r => r.VehicleCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MaintenanceEntryConfiguration : IEntityTypeConfiguration<MaintenanceEntry>
{
    public void Configure(EntityTypeBuilder<MaintenanceEntry> builder)
    {
        builder.ToTable("MaintenanceEntries");

        builder.Property(e => e.Title).HasMaxLength(128);
        builder.Property(e => e.PerformedBy).HasMaxLength(128);
        builder.Property(e => e.Comment).HasMaxLength(2048);

        builder.HasIndex(e => new { e.VehicleId, e.PerformedAt });
        builder.HasIndex(e => e.NextDueDate);
        builder.HasIndex(e => e.NextDueMileage);

        builder.HasOne(e => e.Vehicle)
            .WithMany(v => v.MaintenanceEntries)
            .HasForeignKey(e => e.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Rule)
            .WithMany(r => r.Entries)
            .HasForeignKey(e => e.MaintenanceRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Workshop)
            .WithMany()
            .HasForeignKey(e => e.WorkshopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.WorkshopOrder)
            .WithMany()
            .HasForeignKey(e => e.WorkshopOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
    {
        builder.ToTable("Workshops");

        builder.Property(w => w.Name).HasMaxLength(128).IsRequired();
        builder.Property(w => w.Street).HasMaxLength(128);
        builder.Property(w => w.PostalCode).HasMaxLength(16);
        builder.Property(w => w.City).HasMaxLength(64);
        builder.Property(w => w.Phone).HasMaxLength(32);
        builder.Property(w => w.Email).HasMaxLength(256);
        builder.Property(w => w.ContactPerson).HasMaxLength(128);
        builder.Property(w => w.Comment).HasMaxLength(2048);

        builder.HasIndex(w => w.Name);
    }
}

public sealed class WorkshopOrderConfiguration : IEntityTypeConfiguration<WorkshopOrder>
{
    public void Configure(EntityTypeBuilder<WorkshopOrder> builder)
    {
        builder.ToTable("WorkshopOrders");

        builder.Property(w => w.OrderNumber).HasMaxLength(32).IsRequired();
        builder.Property(w => w.Reason).HasMaxLength(512);
        builder.Property(w => w.WorkToPerform).HasMaxLength(4000);
        builder.Property(w => w.InvoiceNumber).HasMaxLength(64);
        builder.Property(w => w.Comment).HasMaxLength(4000);

        builder.HasIndex(w => w.OrderNumber).IsUnique();
        builder.HasIndex(w => new { w.VehicleId, w.CreatedOn });
        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.AppointmentDate);
        builder.HasIndex(w => w.InvoiceNumber);

        builder.HasOne(w => w.Vehicle)
            .WithMany(v => v.WorkshopOrders)
            .HasForeignKey(w => w.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Driver)
            .WithMany()
            .HasForeignKey(w => w.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(w => w.Workshop)
            .WithMany(s => s.Orders)
            .HasForeignKey(w => w.WorkshopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.InvoiceDocument)
            .WithMany()
            .HasForeignKey(w => w.InvoiceDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class WorkshopTaskConfiguration : IEntityTypeConfiguration<WorkshopTask>
{
    public void Configure(EntityTypeBuilder<WorkshopTask> builder)
    {
        builder.ToTable("WorkshopTasks");

        builder.Property(t => t.Description).HasMaxLength(1024).IsRequired();
        builder.Property(t => t.StandardTaskKey).HasMaxLength(32);
        builder.Property(t => t.Comment).HasMaxLength(1024);

        builder.HasIndex(t => new { t.WorkshopOrderId, t.Position });

        builder.HasOne(t => t.Order)
            .WithMany(o => o.Tasks)
            .HasForeignKey(t => t.WorkshopOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.DamageReport)
            .WithMany()
            .HasForeignKey(t => t.DamageReportId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class WorkshopDocumentConfiguration : IEntityTypeConfiguration<WorkshopDocument>
{
    public void Configure(EntityTypeBuilder<WorkshopDocument> builder)
    {
        builder.ToTable("WorkshopDocuments");

        builder.HasIndex(d => new { d.WorkshopOrderId, d.VehicleDocumentId }).IsUnique();

        builder.HasOne(d => d.Order)
            .WithMany(o => o.Documents)
            .HasForeignKey(d => d.WorkshopOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Document)
            .WithMany()
            .HasForeignKey(d => d.VehicleDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
