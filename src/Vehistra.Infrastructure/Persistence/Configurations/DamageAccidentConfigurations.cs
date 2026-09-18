using Vehistra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vehistra.Infrastructure.Persistence.Configurations;

public sealed class DamageCategoryConfiguration : IEntityTypeConfiguration<DamageCategory>
{
    public void Configure(EntityTypeBuilder<DamageCategory> builder)
    {
        builder.ToTable("DamageCategories");

        builder.Property(c => c.Name).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(512);

        builder.HasIndex(c => c.Name).IsUnique();
    }
}

public sealed class DamageReportConfiguration : IEntityTypeConfiguration<DamageReport>
{
    public void Configure(EntityTypeBuilder<DamageReport> builder)
    {
        builder.ToTable("DamageReports");

        builder.Property(d => d.DamageNumber).HasMaxLength(32).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(2048).IsRequired();
        builder.Property(d => d.InsuranceClaimNumber).HasMaxLength(64);
        builder.Property(d => d.Comment).HasMaxLength(4000);

        builder.HasIndex(d => d.DamageNumber).IsUnique();
        builder.HasIndex(d => new { d.VehicleId, d.OccurredAt });
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.Priority);

        builder.HasOne(d => d.Vehicle)
            .WithMany(v => v.Damages)
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Driver)
            .WithMany()
            .HasForeignKey(d => d.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Category)
            .WithMany(c => c.Damages)
            .HasForeignKey(d => d.DamageCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Workshop)
            .WithMany()
            .HasForeignKey(d => d.WorkshopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.WorkshopOrder)
            .WithMany(o => o.Damages)
            .HasForeignKey(d => d.WorkshopOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.AccidentReport)
            .WithMany(a => a.Damages)
            .HasForeignKey(d => d.AccidentReportId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class DamageAttachmentConfiguration : IEntityTypeConfiguration<DamageAttachment>
{
    public void Configure(EntityTypeBuilder<DamageAttachment> builder)
    {
        builder.ToTable("DamageAttachments");

        builder.Property(a => a.Caption).HasMaxLength(256);

        builder.HasIndex(a => new { a.DamageReportId, a.VehicleDocumentId }).IsUnique();

        builder.HasOne(a => a.DamageReport)
            .WithMany(d => d.Attachments)
            .HasForeignKey(a => a.DamageReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.VehicleDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccidentReportConfiguration : IEntityTypeConfiguration<AccidentReport>
{
    public void Configure(EntityTypeBuilder<AccidentReport> builder)
    {
        builder.ToTable("AccidentReports");

        builder.Property(a => a.AccidentNumber).HasMaxLength(32).IsRequired();
        builder.Property(a => a.DriverPhone).HasMaxLength(32);
        builder.Property(a => a.Location).HasMaxLength(256);
        builder.Property(a => a.Street).HasMaxLength(128);
        builder.Property(a => a.PostalCode).HasMaxLength(16);
        builder.Property(a => a.City).HasMaxLength(64);
        builder.Property(a => a.TypeOther).HasMaxLength(128);
        builder.Property(a => a.CourseOfEvents).HasMaxLength(4000);
        builder.Property(a => a.PoliceStation).HasMaxLength(128);
        builder.Property(a => a.PoliceFileNumber).HasMaxLength(64);
        builder.Property(a => a.OwnInsuranceClaimNumber).HasMaxLength(64);
        builder.Property(a => a.Comment).HasMaxLength(4000);

        builder.HasIndex(a => a.AccidentNumber).IsUnique();
        builder.HasIndex(a => new { a.VehicleId, a.OccurredAt });
        builder.HasIndex(a => a.PoliceFileNumber);

        builder.HasOne(a => a.Vehicle)
            .WithMany(v => v.Accidents)
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Driver)
            .WithMany()
            .HasForeignKey(a => a.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.VehicleInsurance)
            .WithMany()
            .HasForeignKey(a => a.VehicleInsuranceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class AccidentParticipantConfiguration : IEntityTypeConfiguration<AccidentParticipant>
{
    public void Configure(EntityTypeBuilder<AccidentParticipant> builder)
    {
        builder.ToTable("AccidentParticipants");

        builder.Property(p => p.LicensePlate).HasMaxLength(16);
        builder.Property(p => p.LastName).HasMaxLength(64);
        builder.Property(p => p.FirstName).HasMaxLength(64);
        builder.Property(p => p.Phone).HasMaxLength(32);
        builder.Property(p => p.Street).HasMaxLength(128);
        builder.Property(p => p.PostalCode).HasMaxLength(16);
        builder.Property(p => p.City).HasMaxLength(64);
        builder.Property(p => p.InsuranceCompany).HasMaxLength(128);
        builder.Property(p => p.InsuranceNumber).HasMaxLength(64);
        builder.Property(p => p.VehicleDescription).HasMaxLength(128);
        builder.Property(p => p.Comment).HasMaxLength(1024);

        builder.HasOne(p => p.AccidentReport)
            .WithMany(a => a.Participants)
            .HasForeignKey(p => p.AccidentReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AccidentWitnessConfiguration : IEntityTypeConfiguration<AccidentWitness>
{
    public void Configure(EntityTypeBuilder<AccidentWitness> builder)
    {
        builder.ToTable("AccidentWitnesses");

        builder.Property(w => w.LastName).HasMaxLength(64);
        builder.Property(w => w.FirstName).HasMaxLength(64);
        builder.Property(w => w.Phone).HasMaxLength(32);
        builder.Property(w => w.Address).HasMaxLength(256);
        builder.Property(w => w.Comment).HasMaxLength(1024);

        builder.HasOne(w => w.AccidentReport)
            .WithMany(a => a.Witnesses)
            .HasForeignKey(w => w.AccidentReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AccidentAttachmentConfiguration : IEntityTypeConfiguration<AccidentAttachment>
{
    public void Configure(EntityTypeBuilder<AccidentAttachment> builder)
    {
        builder.ToTable("AccidentAttachments");

        builder.Property(a => a.Caption).HasMaxLength(256);

        builder.HasIndex(a => new { a.AccidentReportId, a.VehicleDocumentId }).IsUnique();

        builder.HasOne(a => a.AccidentReport)
            .WithMany(r => r.Attachments)
            .HasForeignKey(a => a.AccidentReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.VehicleDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
