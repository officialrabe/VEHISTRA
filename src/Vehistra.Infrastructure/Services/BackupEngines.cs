using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vehistra.Application.Abstractions;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Der anbieterabhaengige Teil einer Datenbanksicherung. Berechtigungspruefung,
/// Protokollierung und Historie bleiben im <see cref="BackupService"/>.
/// </summary>
internal interface IBackupEngine
{
    /// <summary>Dateiname der Sicherung, ohne Verzeichnis.</summary>
    string BuildFileName(ServerConnectionSettings settings, string kind, DateTime at);

    /// <summary>Setzt Verzeichnis und Dateiname zusammen.</summary>
    string CombinePath(string directory, string fileName);

    /// <summary>Erstellt die Sicherung. Wirft bei Misserfolg eine Ausnahme.</summary>
    Task CreateAsync(
        DatabaseFacade database,
        ServerConnectionSettings settings,
        string targetPath,
        string description,
        CancellationToken cancellationToken);

    /// <summary>Prueft die Sicherung. Wirft bei Misserfolg eine Ausnahme.</summary>
    Task VerifyAsync(DatabaseFacade database, string targetPath, CancellationToken cancellationToken);

    /// <summary>Beschreibt, was geprueft wurde - fuer die Rueckmeldung an den Anwender.</summary>
    string VerifyDescription { get; }
}

/// <summary>
/// Sicherung ueber Microsoft SQL Server: BACKUP DATABASE, geprueft mit
/// RESTORE VERIFYONLY. Die Datei entsteht auf dem Datenbankserver.
/// </summary>
internal sealed class SqlServerBackupEngine : IBackupEngine
{
    public string VerifyDescription => "Die Sicherungsdatei ist lesbar und vollstaendig.";

    public string BuildFileName(ServerConnectionSettings settings, string kind, DateTime at) =>
        $"{settings.Database}_{at:yyyyMMdd_HHmmss}_{kind}.bak";

    /// <summary>Der Pfad gilt auf dem Datenbankserver, daher Windows-Trenner.</summary>
    public string CombinePath(string directory, string fileName) =>
        directory.TrimEnd('\\', '/') + "\\" + fileName;

    public Task CreateAsync(
        DatabaseFacade database,
        ServerConnectionSettings settings,
        string targetPath,
        string description,
        CancellationToken cancellationToken)
    {
        // Pfad und Bezeichnung parameterisiert, der Datenbankname maskiert -
        // ueber beides ist keine SQL-Injection moeglich.
        var sql = $"""
            BACKUP DATABASE [{EscapeIdentifier(settings.Database)}]
            TO DISK = @path
            WITH FORMAT, INIT, NAME = @name, SKIP, NOREWIND, NOUNLOAD, COMPRESSION, STATS = 10
            """;

        return database.ExecuteSqlRawAsync(
            sql,
            [new SqlParameter("@path", targetPath), new SqlParameter("@name", description)],
            cancellationToken);
    }

    public Task VerifyAsync(DatabaseFacade database, string targetPath, CancellationToken cancellationToken) =>
        database.ExecuteSqlRawAsync(
            "RESTORE VERIFYONLY FROM DISK = @path",
            [new SqlParameter("@path", targetPath)],
            cancellationToken);

    private static string EscapeIdentifier(string identifier) => identifier.Replace("]", "]]");
}

/// <summary>
/// Sicherung des Solo-Platzes: VACUUM INTO erzeugt eine in sich stimmige Kopie
/// der Datenbankdatei, auch waehrend das Programm laeuft. Geprueft wird sie mit
/// PRAGMA integrity_check auf der Kopie selbst.
/// </summary>
internal sealed class SqliteBackupEngine : IBackupEngine
{
    public string VerifyDescription => "Die Sicherungsdatei wurde geoeffnet und ist unbeschaedigt.";

    public string BuildFileName(ServerConnectionSettings settings, string kind, DateTime at) =>
        $"Vehistra_{at:yyyyMMdd_HHmmss}_{kind}.db";

    public string CombinePath(string directory, string fileName) =>
        Path.Combine(directory, fileName);

    public async Task CreateAsync(
        DatabaseFacade database,
        ServerConnectionSettings settings,
        string targetPath,
        string description,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath));

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // VACUUM INTO bricht ab, wenn die Zieldatei bereits existiert.
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        await database.ExecuteSqlRawAsync(
            "VACUUM INTO $ziel",
            [new SqliteParameter("$ziel", Path.GetFullPath(targetPath))],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task VerifyAsync(DatabaseFacade database, string targetPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("Die Sicherungsdatei wurde nicht gefunden.", targetPath);
        }

        // Die Kopie wird eigenstaendig geoeffnet - nur so wird sie wirklich geprueft
        // und nicht bloss die laufende Datenbank.
        var verbindung = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(targetPath),
            Mode = SqliteOpenMode.ReadOnly
        }.ConnectionString;

        await using var connection = new SqliteConnection(verbindung);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";

        var ergebnis = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;

        if (!string.Equals(ergebnis, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Die Sicherungsdatei ist beschaedigt. SQLite meldet: {ergebnis ?? "keine Antwort"}");
        }
    }
}
