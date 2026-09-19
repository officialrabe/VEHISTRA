using Vehistra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Vehistra.Application.Abstractions;

/// <summary>
/// Zugriff auf die Fuhrparkdatenbank. Die Implementierung liegt in der Infrastrukturschicht,
/// damit die Anwendungsdienste testbar bleiben.
/// </summary>
public interface IVehistraDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<Vehicle> Vehicles { get; }

    DbSet<VehicleCategory> VehicleCategories { get; }

    DbSet<VehicleCategoryAssignment> VehicleCategoryAssignments { get; }

    DbSet<VehicleStatus> VehicleStatuses { get; }

    DbSet<VehicleStatusHistory> VehicleStatusHistory { get; }

    DbSet<Driver> Drivers { get; }

    DbSet<VehicleDriverAssignment> VehicleDriverAssignments { get; }

    DbSet<VehicleRegistration> VehicleRegistrations { get; }

    DbSet<VehicleRetirement> VehicleRetirements { get; }

    DbSet<MileageEntry> MileageEntries { get; }

    DbSet<VehicleInspection> VehicleInspections { get; }

    DbSet<MaintenanceRule> MaintenanceRules { get; }

    DbSet<MaintenanceEntry> MaintenanceEntries { get; }

    DbSet<Workshop> Workshops { get; }

    DbSet<WorkshopOrder> WorkshopOrders { get; }

    DbSet<WorkshopTask> WorkshopTasks { get; }

    DbSet<WorkshopDocument> WorkshopDocuments { get; }

    DbSet<DamageReport> DamageReports { get; }

    DbSet<DamageCategory> DamageCategories { get; }

    DbSet<DamageAttachment> DamageAttachments { get; }

    DbSet<AccidentReport> AccidentReports { get; }

    DbSet<AccidentParticipant> AccidentParticipants { get; }

    DbSet<AccidentWitness> AccidentWitnesses { get; }

    DbSet<AccidentAttachment> AccidentAttachments { get; }

    DbSet<VehicleDocument> VehicleDocuments { get; }

    DbSet<GeneratedDocument> GeneratedDocuments { get; }

    DbSet<DocumentTemplate> DocumentTemplates { get; }

    DbSet<LicensePlate> LicensePlates { get; }

    DbSet<LicensePlateAssignment> LicensePlateAssignments { get; }

    DbSet<LicensePlateReservation> LicensePlateReservations { get; }

    DbSet<VehicleInsurance> VehicleInsurances { get; }

    DbSet<VehicleKey> VehicleKeys { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<SystemSetting> SystemSettings { get; }

    DbSet<ApplicationVersion> ApplicationVersions { get; }

    DbSet<DatabaseVersion> DatabaseVersions { get; }

    DbSet<MigrationLock> MigrationLocks { get; }

    DbSet<UpdateHistory> UpdateHistory { get; }

    DbSet<BackupHistory> BackupHistory { get; }

    DatabaseFacade Database { get; }

    /// <summary>Speichert alle Aenderungen und schreibt dabei Audit-Eintraege.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Entfernt eine Entitaet aus dem Change-Tracker (z. B. nach einem Konflikt).</summary>
    void DetachAll();
}
