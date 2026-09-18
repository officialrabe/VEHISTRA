using Vehistra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vehistra.Infrastructure.Persistence.Configurations;

public sealed class VehicleRegistrationConfiguration : IEntityTypeConfiguration<VehicleRegistration>
{
    public void Configure(EntityTypeBuilder<VehicleRegistration> builder)
    {
        builder.ToTable("VehicleRegistrations");

        builder.Property(r => r.LicensePlate).HasMaxLength(16);
        builder.Property(r => r.NewLicensePlate).HasMaxLength(16);
        builder.Property(r => r.RegistrationOffice).HasMaxLength(128);
        builder.Property(r => r.Reason).HasMaxLength(256);
        builder.Property(r => r.Comment).HasMaxLength(2048);

        builder.HasIndex(r => new { r.VehicleId, r.RegisteredAt });

        builder.HasOne(r => r.Vehicle)
            .WithMany(v => v.Registrations)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class VehicleRetirementConfiguration : IEntityTypeConfiguration<VehicleRetirement>
{
    public void Configure(EntityTypeBuilder<VehicleRetirement> builder)
    {
        builder.ToTable("VehicleRetirements");

        builder.Property(r => r.ReasonText).HasMaxLength(256);
        builder.Property(r => r.Buyer).HasMaxLength(128);
        builder.Property(r => r.Comment).HasMaxLength(2048);

        builder.HasIndex(r => r.VehicleId).IsUnique();
        builder.HasIndex(r => r.RetiredAt);
    }
}

public sealed class LicensePlateConfiguration : IEntityTypeConfiguration<LicensePlate>
{
    public void Configure(EntityTypeBuilder<LicensePlate> builder)
    {
        builder.ToTable("LicensePlates");

        builder.Property(p => p.Plate).HasMaxLength(16).IsRequired();
        builder.Property(p => p.RegistrationOffice).HasMaxLength(128);
        builder.Property(p => p.Comment).HasMaxLength(2048);
        builder.Property(p => p.SeasonFrom).HasMaxLength(8);
        builder.Property(p => p.SeasonTo).HasMaxLength(8);

        builder.HasIndex(p => p.Plate).IsUnique().HasDatabaseName("IX_LicensePlates_Plate");
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.CurrentVehicle)
            .WithMany()
            .HasForeignKey(p => p.CurrentVehicleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class LicensePlateAssignmentConfiguration : IEntityTypeConfiguration<LicensePlateAssignment>
{
    public void Configure(EntityTypeBuilder<LicensePlateAssignment> builder)
    {
        builder.ToTable("LicensePlateAssignments");

        builder.Property(a => a.Reason).HasMaxLength(256);
        builder.Property(a => a.Comment).HasMaxLength(1024);
        builder.Property(a => a.AssignedByUserName).HasMaxLength(128);

        builder.HasIndex(a => new { a.LicensePlateId, a.ValidFrom });
        builder.HasIndex(a => new { a.VehicleId, a.ValidFrom });

        builder.HasOne(a => a.LicensePlate)
            .WithMany(p => p.Assignments)
            .HasForeignKey(a => a.LicensePlateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Vehicle)
            .WithMany(v => v.LicensePlateAssignments)
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LicensePlateReservationConfiguration : IEntityTypeConfiguration<LicensePlateReservation>
{
    public void Configure(EntityTypeBuilder<LicensePlateReservation> builder)
    {
        builder.ToTable("LicensePlateReservations");

        builder.Property(r => r.RegistrationOffice).HasMaxLength(128);
        builder.Property(r => r.ReservationNumber).HasMaxLength(64);
        builder.Property(r => r.PinEncrypted).HasMaxLength(512);
        builder.Property(r => r.Comment).HasMaxLength(1024);

        builder.HasIndex(r => new { r.LicensePlateId, r.IsReleased });
        builder.HasIndex(r => r.ReservedUntil);

        builder.HasOne(r => r.LicensePlate)
            .WithMany(p => p.Reservations)
            .HasForeignKey(r => r.LicensePlateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class VehicleInsuranceConfiguration : IEntityTypeConfiguration<VehicleInsurance>
{
    public void Configure(EntityTypeBuilder<VehicleInsurance> builder)
    {
        builder.ToTable("VehicleInsurances");

        builder.Property(i => i.Company).HasMaxLength(128).IsRequired();
        builder.Property(i => i.PolicyNumber).HasMaxLength(64);
        builder.Property(i => i.ContractNumber).HasMaxLength(64);
        builder.Property(i => i.ContactPerson).HasMaxLength(128);
        builder.Property(i => i.Phone).HasMaxLength(32);
        builder.Property(i => i.Email).HasMaxLength(256);
        builder.Property(i => i.Comment).HasMaxLength(2048);

        builder.HasIndex(i => new { i.VehicleId, i.IsActive });
        builder.HasIndex(i => i.ValidTo);

        builder.HasOne(i => i.Vehicle)
            .WithMany(v => v.Insurances)
            .HasForeignKey(i => i.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class VehicleKeyConfiguration : IEntityTypeConfiguration<VehicleKey>
{
    public void Configure(EntityTypeBuilder<VehicleKey> builder)
    {
        builder.ToTable("VehicleKeys");

        builder.Property(k => k.KeyNumber).HasMaxLength(32).IsRequired();
        builder.Property(k => k.StorageLocation).HasMaxLength(128);
        builder.Property(k => k.IssuedToName).HasMaxLength(128);
        builder.Property(k => k.Comment).HasMaxLength(1024);

        builder.HasIndex(k => new { k.VehicleId, k.KeyNumber }).IsUnique();

        builder.HasOne(k => k.Vehicle)
            .WithMany(v => v.Keys)
            .HasForeignKey(k => k.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(k => k.IssuedToDriver)
            .WithMany()
            .HasForeignKey(k => k.IssuedToDriverId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.ToTable("VehicleDocuments");

        builder.Property(d => d.Title).HasMaxLength(256).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(2048);
        builder.Property(d => d.OriginalFileName).HasMaxLength(256).IsRequired();
        builder.Property(d => d.RelativePath).HasMaxLength(512).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(128);
        builder.Property(d => d.Sha256).HasMaxLength(64);

        builder.HasIndex(d => new { d.VehicleId, d.Category });
        builder.HasIndex(d => d.CreatedAt);

        builder.HasOne(d => d.Vehicle)
            .WithMany(v => v.Documents)
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GeneratedDocumentConfiguration : IEntityTypeConfiguration<GeneratedDocument>
{
    public void Configure(EntityTypeBuilder<GeneratedDocument> builder)
    {
        builder.ToTable("GeneratedDocuments");

        builder.Property(d => d.TemplateKey).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Title).HasMaxLength(256).IsRequired();
        builder.Property(d => d.GeneratedByUserName).HasMaxLength(128);

        builder.HasIndex(d => new { d.TemplateKey, d.GeneratedAt });

        builder.HasOne(d => d.Vehicle).WithMany().HasForeignKey(d => d.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.WorkshopOrder).WithMany().HasForeignKey(d => d.WorkshopOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.AccidentReport).WithMany().HasForeignKey(d => d.AccidentReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Document).WithMany().HasForeignKey(d => d.VehicleDocumentId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.ToTable("DocumentTemplates");

        builder.Property(t => t.Key).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(1024);
        builder.Property(t => t.HeaderText).HasMaxLength(512);
        builder.Property(t => t.FooterText).HasMaxLength(512);
        builder.Property(t => t.NoticeText).HasMaxLength(2048);

        builder.HasIndex(t => t.Key).IsUnique();
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.Property(n => n.Title).HasMaxLength(256).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2048).IsRequired();
        builder.Property(n => n.SourceReference).HasMaxLength(128);
        builder.Property(n => n.DeduplicationKey).HasMaxLength(128);

        builder.HasIndex(n => new { n.IsDismissed, n.IsRead, n.CreatedAt });
        builder.HasIndex(n => n.DeduplicationKey);

        builder.HasOne(n => n.TargetUser).WithMany().HasForeignKey(n => n.TargetUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.TargetRole).WithMany().HasForeignKey(n => n.TargetRoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.Vehicle).WithMany().HasForeignKey(n => n.VehicleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.Property(a => a.UserName).HasMaxLength(64);
        builder.Property(a => a.ComputerName).HasMaxLength(128);
        builder.Property(a => a.EntityName).HasMaxLength(64).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.EntityDisplay).HasMaxLength(256);
        builder.Property(a => a.OldValues).HasMaxLength(4000);
        builder.Property(a => a.NewValues).HasMaxLength(4000);
        builder.Property(a => a.AdditionalInfo).HasMaxLength(1024);
        builder.Property(a => a.ApplicationVersion).HasMaxLength(32);

        builder.HasIndex(a => a.Timestamp).HasDatabaseName("IX_AuditLogs_Timestamp");
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.UserName);
    }
}

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");

        builder.Property(s => s.Key).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Value).HasMaxLength(2048);
        builder.Property(s => s.Description).HasMaxLength(512);
        builder.Property(s => s.Category).HasMaxLength(64).IsRequired();
        builder.Property(s => s.DataType).HasMaxLength(32).IsRequired();

        builder.HasIndex(s => s.Key).IsUnique();
    }
}

public sealed class ApplicationVersionConfiguration : IEntityTypeConfiguration<ApplicationVersion>
{
    public void Configure(EntityTypeBuilder<ApplicationVersion> builder)
    {
        builder.ToTable("ApplicationVersions");

        builder.Property(v => v.Version).HasMaxLength(32).IsRequired();
        builder.Property(v => v.ReleaseNotes).HasMaxLength(4000);
        builder.Property(v => v.MinimumDatabaseVersion).HasMaxLength(32);

        builder.HasIndex(v => v.Version).IsUnique();
    }
}

public sealed class DatabaseVersionConfiguration : IEntityTypeConfiguration<DatabaseVersion>
{
    public void Configure(EntityTypeBuilder<DatabaseVersion> builder)
    {
        builder.ToTable("DatabaseVersions");

        builder.Property(v => v.SchemaVersion).HasMaxLength(32).IsRequired();
        builder.Property(v => v.LastMigration).HasMaxLength(256);
        builder.Property(v => v.AppliedByComputer).HasMaxLength(128);
        builder.Property(v => v.AppliedByUserName).HasMaxLength(64);
        builder.Property(v => v.ApplicationVersion).HasMaxLength(32);
        builder.Property(v => v.MinimumSupportedApplicationVersion).HasMaxLength(32);

        builder.HasIndex(v => v.AppliedAt);
        builder.HasIndex(v => v.IsCurrent);
    }
}

public sealed class MigrationLockConfiguration : IEntityTypeConfiguration<MigrationLock>
{
    public void Configure(EntityTypeBuilder<MigrationLock> builder)
    {
        builder.ToTable("MigrationLocks");

        builder.Property(l => l.LockKey).HasMaxLength(64).IsRequired();
        builder.Property(l => l.LockedByComputer).HasMaxLength(128);
        builder.Property(l => l.LockedByUserName).HasMaxLength(64);
        builder.Property(l => l.LockedByProcess).HasMaxLength(128);
        builder.Property(l => l.Comment).HasMaxLength(512);

        builder.HasIndex(l => l.LockKey).IsUnique();
    }
}

public sealed class UpdateHistoryConfiguration : IEntityTypeConfiguration<UpdateHistory>
{
    public void Configure(EntityTypeBuilder<UpdateHistory> builder)
    {
        builder.ToTable("UpdateHistory");

        builder.Property(u => u.ComputerName).HasMaxLength(128);
        builder.Property(u => u.UserName).HasMaxLength(64);
        builder.Property(u => u.OldApplicationVersion).HasMaxLength(32);
        builder.Property(u => u.NewApplicationVersion).HasMaxLength(32);
        builder.Property(u => u.OldDatabaseVersion).HasMaxLength(32);
        builder.Property(u => u.NewDatabaseVersion).HasMaxLength(32);
        builder.Property(u => u.BackupPath).HasMaxLength(512);
        builder.Property(u => u.ErrorMessage).HasMaxLength(2048);
        builder.Property(u => u.Details).HasMaxLength(4000);

        builder.HasIndex(u => u.StartedAt);
    }
}

public sealed class BackupHistoryConfiguration : IEntityTypeConfiguration<BackupHistory>
{
    public void Configure(EntityTypeBuilder<BackupHistory> builder)
    {
        builder.ToTable("BackupHistory");

        builder.Property(b => b.FilePath).HasMaxLength(512);
        builder.Property(b => b.TriggeredByUserName).HasMaxLength(64);
        builder.Property(b => b.ComputerName).HasMaxLength(128);
        builder.Property(b => b.Kind).HasMaxLength(32).IsRequired();
        builder.Property(b => b.ErrorMessage).HasMaxLength(2048);

        builder.HasIndex(b => b.StartedAt);
    }
}
