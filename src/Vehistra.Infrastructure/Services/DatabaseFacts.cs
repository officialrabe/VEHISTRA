using Microsoft.EntityFrameworkCore;
using Vehistra.Application.Abstractions;
using Vehistra.Infrastructure.Persistence;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Auskuenfte ueber die Datenbank, die je Anbieter anders ermittelt werden.
/// Liegt hier, damit Einrichtung und Diagnose nicht jeweils eigene Verzweigungen
/// mitschleppen muessen.
/// </summary>
public static class DatabaseFacts
{
    /// <summary>Anzahl der Tabellen - ein Hinweis darauf, ob die Struktur eingerichtet ist.</summary>
    public static async Task<int> GetTableCountAsync(
        VehistraDbContext db,
        DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        var sql = provider == DatabaseProvider.Sqlite
            ? "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"
            : "SELECT COUNT(*) AS Value FROM sys.tables";

        return await db.Database
            .SqlQueryRaw<int>(sql)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Belegter Platz in Megabyte, oder <c>null</c>, wenn nicht ermittelbar.</summary>
    public static async Task<decimal?> GetSizeMegabytesAsync(
        VehistraDbContext db,
        ServerConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.Provider == DatabaseProvider.Sqlite)
        {
            // Beim Solo-Platz ist die Datenbank eine Datei - die Groesse steht
            // im Dateisystem und muss nicht abgefragt werden.
            if (string.IsNullOrWhiteSpace(settings.DatabaseFile) || !File.Exists(settings.DatabaseFile))
            {
                return null;
            }

            // Solange das Programm laeuft, stehen die juengsten Aenderungen im
            // Write-Ahead-Log neben der eigentlichen Datei. Wer nur die .db
            // misst, meldet einer gefuellten Datenbank "0,00 MB".
            var bytes = SumFileSizes(
                settings.DatabaseFile,
                settings.DatabaseFile + "-wal",
                settings.DatabaseFile + "-journal");

            return Math.Round(bytes / 1024m / 1024m, 2);
        }

        try
        {
            return await db.Database
                .SqlQueryRaw<decimal>(
                    "SELECT CAST(SUM(size) * 8.0 / 1024 AS decimal(18,2)) AS Value FROM sys.database_files")
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Prueft, ob gelesen und geschrieben werden darf. Bei SQL Server ueber die
    /// Rechteverwaltung, beim Solo-Platz durch einen tatsaechlichen Schreibversuch
    /// in einer Transaktion, die anschliessend verworfen wird.
    /// </summary>
    public static async Task<(bool CanRead, bool CanWrite)> GetPermissionsAsync(
        VehistraDbContext db,
        DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (provider == DatabaseProvider.Sqlite)
        {
            var lesen = await TryAsync(() => db.Users.AnyAsync(cancellationToken)).ConfigureAwait(false);
            var schreiben = await TrySqliteWriteAsync(db, cancellationToken).ConfigureAwait(false);

            return (lesen, schreiben);
        }

        return (
            await HasSqlServerPermissionAsync(db, "SELECT", cancellationToken).ConfigureAwait(false),
            await HasSqlServerPermissionAsync(db, "INSERT", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Summiert vorhandene Dateien; fehlende zaehlen als null.</summary>
    private static long SumFileSizes(params string[] paths)
    {
        long summe = 0;

        foreach (var pfad in paths)
        {
            try
            {
                if (File.Exists(pfad))
                {
                    summe += new FileInfo(pfad).Length;
                }
            }
            catch (IOException)
            {
                // Eine gesperrte Nebendatei macht die Angabe nur ungenauer,
                // nicht unbrauchbar.
            }
        }

        return summe;
    }

    private static async Task<bool> HasSqlServerPermissionAsync(
        VehistraDbContext db,
        string permission,
        CancellationToken cancellationToken)
    {
        // Ausschliesslich feste Abfragetexte - niemals zusammengesetztes SQL.
        var sql = permission switch
        {
            "SELECT" => "SELECT HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'SELECT') AS Value",
            "INSERT" => "SELECT HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'INSERT') AS Value",
            _ => throw new ArgumentOutOfRangeException(nameof(permission))
        };

        try
        {
            var value = await db.Database
                .SqlQueryRaw<int?>(sql)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return value == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Schreibversuch, der garantiert nichts hinterlaesst.</summary>
    private static async Task<bool> TrySqliteWriteAsync(VehistraDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaktion = await db.Database
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS __schreibtest (wert INTEGER)", cancellationToken)
                .ConfigureAwait(false);

            await db.Database.ExecuteSqlRawAsync("DROP TABLE __schreibtest", cancellationToken)
                .ConfigureAwait(false);

            await transaktion.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> TryAsync(Func<Task<bool>> aktion)
    {
        try
        {
            await aktion().ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
