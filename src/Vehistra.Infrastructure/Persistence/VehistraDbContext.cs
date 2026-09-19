using System.Reflection;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Common;
using Vehistra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Infrastructure.Persistence;

/// <summary>
/// Datenbankkontext der Fuhrparkdatenbank.
/// Zielsystem ist Microsoft SQL Server bzw. SQL Server Express.
/// </summary>
public class VehistraDbContext : DbContext, IVehistraDbContext
{
    public VehistraDbContext(DbContextOptions<VehistraDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();

    public DbSet<VehicleCategoryAssignment> VehicleCategoryAssignments => Set<VehicleCategoryAssignment>();

    public DbSet<VehicleStatus> VehicleStatuses => Set<VehicleStatus>();

    public DbSet<VehicleStatusHistory> VehicleStatusHistory => Set<VehicleStatusHistory>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<VehicleDriverAssignment> VehicleDriverAssignments => Set<VehicleDriverAssignment>();

    public DbSet<VehicleRegistration> VehicleRegistrations => Set<VehicleRegistration>();

    public DbSet<VehicleRetirement> VehicleRetirements => Set<VehicleRetirement>();

    public DbSet<MileageEntry> MileageEntries => Set<MileageEntry>();

    public DbSet<VehicleInspection> VehicleInspections => Set<VehicleInspection>();

    public DbSet<MaintenanceRule> MaintenanceRules => Set<MaintenanceRule>();

    public DbSet<MaintenanceEntry> MaintenanceEntries => Set<MaintenanceEntry>();

    public DbSet<Workshop> Workshops => Set<Workshop>();

    public DbSet<WorkshopOrder> WorkshopOrders => Set<WorkshopOrder>();

    public DbSet<WorkshopTask> WorkshopTasks => Set<WorkshopTask>();

    public DbSet<WorkshopDocument> WorkshopDocuments => Set<WorkshopDocument>();

    public DbSet<DamageReport> DamageReports => Set<DamageReport>();

    public DbSet<DamageCategory> DamageCategories => Set<DamageCategory>();

    public DbSet<DamageAttachment> DamageAttachments => Set<DamageAttachment>();

    public DbSet<AccidentReport> AccidentReports => Set<AccidentReport>();

    public DbSet<AccidentParticipant> AccidentParticipants => Set<AccidentParticipant>();

    public DbSet<AccidentWitness> AccidentWitnesses => Set<AccidentWitness>();

    public DbSet<AccidentAttachment> AccidentAttachments => Set<AccidentAttachment>();

    public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();

    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();

    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();

    public DbSet<LicensePlate> LicensePlates => Set<LicensePlate>();

    public DbSet<LicensePlateAssignment> LicensePlateAssignments => Set<LicensePlateAssignment>();

    public DbSet<LicensePlateReservation> LicensePlateReservations => Set<LicensePlateReservation>();

    public DbSet<VehicleInsurance> VehicleInsurances => Set<VehicleInsurance>();

    public DbSet<VehicleKey> VehicleKeys => Set<VehicleKey>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<ApplicationVersion> ApplicationVersions => Set<ApplicationVersion>();

    public DbSet<DatabaseVersion> DatabaseVersions => Set<DatabaseVersion>();

    public DbSet<MigrationLock> MigrationLocks => Set<MigrationLock>();

    public DbSet<UpdateHistory> UpdateHistory => Set<UpdateHistory>();

    public DbSet<BackupHistory> BackupHistory => Set<BackupHistory>();

    public void DetachAll()
    {
        ChangeTracker.Clear();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Nebenlaeufigkeitstoken: unter SQL Server ein echtes rowversion,
        // unter anderen Anbietern (Tests) ein manuell gepflegtes Token.
        var isSqlServer = Database.IsSqlServer();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var property = modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.RowVersion));

            if (isSqlServer)
            {
                property.IsRowVersion();
            }
            else
            {
                property.IsConcurrencyToken().ValueGeneratedNever();
            }

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AuditableEntity.CreatedByUserName))
                .HasMaxLength(128);

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AuditableEntity.ModifiedByUserName))
                .HasMaxLength(128);
        }

        var lockVersion = modelBuilder.Entity<MigrationLock>().Property(l => l.RowVersion);
        if (isSqlServer)
        {
            lockVersion.IsRowVersion();
        }
        else
        {
            lockVersion.IsConcurrencyToken().ValueGeneratedNever();
        }

        // Standardgenauigkeit fuer Geldbetraege.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        // Zeichenketten ohne explizite Laenge auf 256 begrenzen, damit keine nvarchar(max)-Spalten entstehen.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is null))
        {
            property.SetMaxLength(256);
        }
    }
}
