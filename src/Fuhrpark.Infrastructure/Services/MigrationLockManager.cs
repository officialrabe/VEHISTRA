using System.Diagnostics;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fuhrpark.Infrastructure.Services;

/// <summary>
/// Zentrale Sperre, damit niemals zwei Arbeitsplaetze gleichzeitig eine Datenbankmigration
/// ausfuehren. Die Sperre laeuft automatisch ab, falls ein Client abstuerzt.
/// </summary>
public sealed class MigrationLockManager
{
    public const string LockKey = "SCHEMA_MIGRATION";

    private static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(30);

    private readonly FuhrparkDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<MigrationLockManager> _logger;

    public MigrationLockManager(FuhrparkDbContext db, IClock clock, ILogger<MigrationLockManager> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Gibt zurueck, ob derzeit eine Migration laeuft.</summary>
    public async Task<MigrationLockState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var entry = await _db.MigrationLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LockKey == LockKey, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null || !entry.IsActive(_clock.Now))
        {
            return new MigrationLockState(false, null, null, null);
        }

        return new MigrationLockState(true, entry.LockedByComputer, entry.LockedByUserName, entry.LockedAt);
    }

    /// <summary>
    /// Versucht, die Sperre zu erwerben. Gibt <c>null</c> zurueck, wenn ein anderer Client
    /// die Sperre haelt.
    /// </summary>
    public async Task<MigrationLockHandle?> TryAcquireAsync(
        string? userName,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.Now;

        var entry = await _db.MigrationLocks
            .FirstOrDefaultAsync(l => l.LockKey == LockKey, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            entry = new MigrationLock { LockKey = LockKey };
            _db.MigrationLocks.Add(entry);
        }

        if (entry.IsActive(now))
        {
            _logger.LogWarning(
                "Migrationssperre wird bereits von {Computer} ({User}) gehalten.",
                entry.LockedByComputer, entry.LockedByUserName);
            return null;
        }

        entry.IsLocked = true;
        entry.LockedAt = now;
        entry.LockedByComputer = Environment.MachineName;
        entry.LockedByUserName = userName ?? Environment.UserName;
        entry.LockedByProcess = $"{Process.GetCurrentProcess().ProcessName}:{Environment.ProcessId}";
        entry.ExpiresAt = now.Add(duration ?? DefaultLockDuration);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Ein anderer Client war schneller.
            _db.ChangeTracker.Clear();
            return null;
        }

        _logger.LogInformation("Migrationssperre erworben durch {Computer}.", entry.LockedByComputer);
        return new MigrationLockHandle(this, cancellationToken);
    }

    /// <summary>
    /// Wartet, bis die Sperre erworben werden kann, laengstens bis zum Zeitlimit.
    /// </summary>
    public async Task<MigrationLockHandle?> AcquireAsync(
        string? userName,
        TimeSpan timeout,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = _clock.Now.Add(timeout);
        var informed = false;

        while (_clock.Now < deadline)
        {
            var handle = await TryAcquireAsync(userName, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (handle is not null)
            {
                return handle;
            }

            if (!informed)
            {
                progress?.Report("Die Fuhrparkdatenbank wird derzeit aktualisiert. Bitte warten.");
                informed = true;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
        }

        return null;
    }

    /// <summary>Gibt die Sperre wieder frei.</summary>
    public async Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        var entry = await _db.MigrationLocks
            .FirstOrDefaultAsync(l => l.LockKey == LockKey, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return;
        }

        entry.IsLocked = false;
        entry.LockedAt = null;
        entry.ExpiresAt = null;
        entry.LockedByProcess = null;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Migrationssperre freigegeben.");
    }
}

/// <summary>Zustand der Migrationssperre.</summary>
public sealed record MigrationLockState(bool IsLocked, string? Computer, string? UserName, DateTime? Since);

/// <summary>Gibt die Sperre beim Verlassen des using-Blocks automatisch frei.</summary>
public sealed class MigrationLockHandle : IAsyncDisposable
{
    private readonly MigrationLockManager _manager;
    private readonly CancellationToken _cancellationToken;
    private bool _released;

    internal MigrationLockHandle(MigrationLockManager manager, CancellationToken cancellationToken)
    {
        _manager = manager;
        _cancellationToken = cancellationToken;
    }

    public async ValueTask DisposeAsync()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        await _manager.ReleaseAsync(_cancellationToken).ConfigureAwait(false);
    }
}
