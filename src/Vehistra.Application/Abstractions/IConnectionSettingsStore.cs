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

/// <summary>Verbindungs- und Pfadangaben eines Arbeitsplatzes.</summary>
public sealed class ServerConnectionSettings
{
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

    public ServerConnectionSettings Clone() => (ServerConnectionSettings)MemberwiseClone();
}
