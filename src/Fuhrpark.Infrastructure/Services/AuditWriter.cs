using Fuhrpark.Application.Abstractions;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;
using System.Reflection;
using Fuhrpark.Infrastructure.Persistence;

namespace Fuhrpark.Infrastructure.Services;

/// <summary>Schreibt Audit-Eintraege fuer Vorgaenge ohne Entitaetsaenderung (Login, Druck, Export).</summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly FuhrparkDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly string _applicationVersion;

    public AuditWriter(
        FuhrparkDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        ApplicationVersionProvider versionProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _applicationVersion = versionProvider.Version;
    }

    public async Task WriteAsync(
        AuditAction action,
        string entityName,
        string? entityId,
        string? entityDisplay,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = _clock.Now,
            UserId = _currentUser.User?.Id,
            UserName = _currentUser.User?.UserName,
            ComputerName = _currentUser.ComputerName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            EntityDisplay = entityDisplay,
            AdditionalInfo = additionalInfo,
            ApplicationVersion = _applicationVersion
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Stellt die Programmversion bereit (aus der Assembly oder gesetzt durch den Host).</summary>
public sealed class ApplicationVersionProvider
{
    public ApplicationVersionProvider(string? version = null)
    {
        Version = version
            ?? Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion.Split('+')[0]
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
            ?? "1.0.0";
    }

    public string Version { get; }
}
