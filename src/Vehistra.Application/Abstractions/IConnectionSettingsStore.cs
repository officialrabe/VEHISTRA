namespace Vehistra.Application.Abstractions;

/// <summary>
/// Speichert und laedt die Serververbindung. Passwoerter werden ausschliesslich
/// mit Windows DPAPI verschluesselt abgelegt, niemals im Klartext.
/// </summary>
public interface IConnectionSettingsStore
{
    /// <summary>Maschinenweite Konfiguration unter ProgramData.</summary>
    string ConfigFilePath { get; }

    bool IsConfigured { get; }

    ServerConnectionSettings? Load();

    void Save(ServerConnectionSettings settings);

    /// <summary>Erzeugt den fertigen Connection String inklusive entschluesseltem Passwort.</summary>
    string BuildConnectionString(ServerConnectionSettings settings);

    /// <summary>Exportiert eine Firmenkonfiguration (.fmcfg) - ohne Passwoerter.</summary>
    void ExportClientConfiguration(ServerConnectionSettings settings, string targetPath);

    /// <summary>Importiert eine Firmenkonfiguration (.fmcfg).</summary>
    ServerConnectionSettings ImportClientConfiguration(string sourcePath);
}

/// <summary>Welche Datenbank verwendet wird.</summary>
public enum DatabaseProvider
{
    /// <summary>Microsoft SQL Server im Firmennetz - fuer den Mehrplatzbetrieb.</summary>
    SqlServer = 0,

    /// <summary>
    /// SQLite als Datei auf diesem Computer. Braucht keine Installation und
    /// keinen Dienst, eignet sich aber ausschliesslich fuer den Solo-Platz:
    /// ueber eine Netzwerkfreigabe ist die Dateisperrung nicht verlaesslich.
    /// </summary>
    Sqlite = 1
}

/// <summary>Verbindungs- und Pfadangaben eines Arbeitsplatzes.</summary>
public sealed class ServerConnectionSettings
{
    /// <summary>Welche Datenbank verwendet wird.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    /// <summary>
    /// Datenbankdatei beim Solo-Platz, z. B.
    /// C:\ProgramData\LSP Virtual Services\Vehistra\Vehistra.db.
    /// Nur bei <see cref="DatabaseProvider.Sqlite"/> von Bedeutung.
    /// </summary>
    public string? DatabaseFile { get; set; }

    /// <summary>Servername inklusive Instanz, z. B. FUHRPARK-SRV01\SQLEXPRESS.</summary>
    public string Server { get; set; } = string.Empty;

    public string Database { get; set; } = "VehistraDB";

    /// <summary>Windows-Authentifizierung bevorzugen.</summary>
    public bool UseWindowsAuthentication { get; set; } = true;

    public string? SqlUserName { get; set; }

    /// <summary>Mit DPAPI verschluesseltes Passwort (Base64). Niemals Klartext.</summary>
    public string? ProtectedSqlPassword { get; set; }

    /// <summary>UNC-Pfad der Dokumentenablage.</summary>
    public string? DocumentsPath { get; set; }

    /// <summary>UNC-Pfad der Updateablage.</summary>
    public string? UpdatePath { get; set; }

    /// <summary>Verzeichnis fuer Datenbanksicherungen (serverlokaler Pfad).</summary>
    public string? BackupPath { get; set; }

    public int ConnectTimeoutSeconds { get; set; } = 15;

    public int CommandTimeoutSeconds { get; set; } = 60;

    public bool TrustServerCertificate { get; set; } = true;

    public bool Encrypt { get; set; } = true;

    public DateTime? ConfiguredAt { get; set; }

    public string? ConfiguredBy { get; set; }

    /// <summary>Solo-Platz mit Datenbankdatei statt Server.</summary>
    public bool IsSingleWorkstation => Provider == DatabaseProvider.Sqlite;

    /// <summary>Kurzbeschreibung fuer Anzeige und Protokoll - ohne Zugangsdaten.</summary>
    public string Describe() => Provider switch
    {
        DatabaseProvider.Sqlite => $"Solo-Platz · {DatabaseFile}",
        _ => $"{Server} · {Database}"
    };

    public ServerConnectionSettings Clone() => (ServerConnectionSettings)MemberwiseClone();
}
