using System.Text.Json;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Vehistra.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Schreibt Erstell- und Aenderungsinformationen sowie Audit-Eintraege.
/// Der Audit-Eintrag entsteht automatisch beim Speichern, damit keine Aenderung
/// unprotokolliert bleibt.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <summary>Entitaeten, deren Aenderungen protokolliert werden.</summary>
    private static readonly HashSet<string> AuditedEntities =
    [
        nameof(Vehicle), nameof(VehicleStatusHistory), nameof(VehicleCategoryAssignment),
        nameof(Driver), nameof(VehicleDriverAssignment),
        nameof(LicensePlate), nameof(LicensePlateAssignment), nameof(LicensePlateReservation),
        nameof(VehicleRegistration), nameof(VehicleRetirement),
        nameof(VehicleInspection), nameof(MaintenanceRule), nameof(MaintenanceEntry),
        nameof(DamageReport), nameof(AccidentReport), nameof(AccidentParticipant),
        nameof(WorkshopOrder), nameof(WorkshopTask), nameof(Workshop),
        nameof(VehicleInsurance), nameof(VehicleKey), nameof(VehicleDocument),
        nameof(User), nameof(Role), nameof(RolePermission), nameof(UserRole),
        nameof(SystemSetting), nameof(MileageEntry)
    ];

    /// <summary>Felder, die niemals im Audit-Log erscheinen duerfen.</summary>
    private static readonly HashSet<string> SensitiveProperties =
    [
        nameof(User.PasswordHash),
        nameof(LicensePlateReservation.PinEncrypted),
        nameof(AuditableEntity.RowVersion)
    ];

    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly string _applicationVersion;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser, IClock clock, string applicationVersion)
    {
        _currentUser = currentUser;
        _clock = clock;
        _applicationVersion = applicationVersion;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.Now;
        var user = _currentUser.User;
        var auditEntries = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            if (entry.Entity is AuditableEntity auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = auditable.CreatedAt == default ? now : auditable.CreatedAt;
                        auditable.CreatedByUserId ??= user?.Id;
                        auditable.CreatedByUserName ??= user?.DisplayName;
                        break;

                    case EntityState.Modified:
                        auditable.ModifiedAt = now;
                        auditable.ModifiedByUserId = user?.Id;
                        auditable.ModifiedByUserName = user?.DisplayName;
                        break;
                }
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var entityName = entry.Metadata.ClrType.Name;
            if (!AuditedEntities.Contains(entityName))
            {
                continue;
            }

            var auditEntry = BuildAuditEntry(entry, entityName, now, user?.Id, user?.UserName);
            if (auditEntry is not null)
            {
                auditEntries.Add(auditEntry);
            }
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }
    }

    private AuditLog? BuildAuditEntry(
        EntityEntry entry,
        string entityName,
        DateTime now,
        int? userId,
        string? userName)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Deleted => AuditAction.Deleted,
            _ => AuditAction.Updated
        };

        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;

            if (SensitiveProperties.Contains(name))
            {
                // Aenderung wird vermerkt, der Wert selbst niemals.
                if (entry.State == EntityState.Modified && property.IsModified)
                {
                    oldValues[name] = "***";
                    newValues[name] = "***";
                }

                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    if (property.CurrentValue is not null)
                    {
                        newValues[name] = property.CurrentValue;
                    }

                    break;

                case EntityState.Deleted:
                    oldValues[name] = property.OriginalValue;
                    break;

                case EntityState.Modified when property.IsModified
                                               && !Equals(property.OriginalValue, property.CurrentValue):
                    oldValues[name] = property.OriginalValue;
                    newValues[name] = property.CurrentValue;
                    break;
            }
        }

        if (entry.State == EntityState.Modified && newValues.Count == 0)
        {
            return null;
        }

        var key = entry.Metadata.FindPrimaryKey();
        var id = key is null
            ? null
            : string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));

        return new AuditLog
        {
            Timestamp = now,
            UserId = userId,
            UserName = userName,
            ComputerName = _currentUser.ComputerName,
            Action = action,
            EntityName = entityName,
            EntityId = id,
            EntityDisplay = DescribeEntity(entry.Entity),
            OldValues = Serialize(oldValues),
            NewValues = Serialize(newValues),
            ApplicationVersion = _applicationVersion
        };
    }

    private static string? Serialize(Dictionary<string, object?> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(values, JsonOptions);
        return json.Length > 4000 ? json[..3997] + "..." : json;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Lesbare Bezeichnung des Datensatzes fuer das Audit-Log.</summary>
    private static string? DescribeEntity(object entity) => entity switch
    {
        Vehicle vehicle => vehicle.DisplayName,
        Driver driver => driver.DisplayName,
        User user => user.UserName,
        Role role => role.Name,
        LicensePlate plate => plate.Plate,
        DamageReport damage => damage.DamageNumber,
        AccidentReport accident => accident.AccidentNumber,
        WorkshopOrder order => order.OrderNumber,
        Workshop workshop => workshop.Name,
        VehicleDocument document => document.Title,
        SystemSetting setting => setting.Key,
        VehicleInsurance insurance => insurance.Company,
        VehicleKey key => key.KeyNumber,
        MaintenanceRule rule => rule.Name,
        _ => null
    };
}
