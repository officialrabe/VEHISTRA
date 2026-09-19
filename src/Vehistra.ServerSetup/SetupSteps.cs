namespace Vehistra.ServerSetup;

/// <summary>Schritte des Einrichtungsassistenten.</summary>
public enum SetupStep
{
    SystemCheck = 1,
    DetectSqlServer = 2,
    TestConnection = 3,
    CreateDatabase = 4,
    ApplyMigrations = 5,
    DocumentsFolder = 6,
    BackupFolder = 7,
    UpdateFolder = 8,
    NetworkShares = 9,
    Administrator = 10,
    FinalCheck = 11,
    ClientConfiguration = 12
}

/// <summary>Beschreibung eines Schritts fuer die Fortschrittsanzeige.</summary>
public sealed record SetupStepInfo(SetupStep Step, string Title, string Description)
{
    public int Number => (int)Step;

    public static IReadOnlyList<SetupStepInfo> All { get; } =
    [
        new(SetupStep.SystemCheck, "Systemprüfung",
            "Betriebssystem, Rechte und Voraussetzungen werden geprüft."),
        new(SetupStep.DetectSqlServer, "SQL Server erkennen",
            "Installierte SQL-Server-Instanzen werden gesucht."),
        new(SetupStep.TestConnection, "Verbindung testen",
            "Die Verbindung zum SQL Server wird hergestellt."),
        new(SetupStep.CreateDatabase, "Datenbank",
            "Die Fuhrparkdatenbank wird angelegt oder erkannt."),
        new(SetupStep.ApplyMigrations, "Datenbankstruktur",
            "Tabellen und Stammdaten werden eingerichtet."),
        new(SetupStep.DocumentsFolder, "Dokumentenordner",
            "Der Ordner für Fahrzeugdokumente wird festgelegt."),
        new(SetupStep.BackupFolder, "Backupordner",
            "Der Ordner für Datenbanksicherungen wird festgelegt."),
        new(SetupStep.UpdateFolder, "Updateordner",
            "Der Ordner für Softwareupdates wird festgelegt."),
        new(SetupStep.NetworkShares, "Netzwerkfreigaben",
            "Erreichbarkeit und Schreibrechte werden geprüft."),
        new(SetupStep.Administrator, "Erster Administrator",
            "Das erste Benutzerkonto wird angelegt."),
        new(SetupStep.FinalCheck, "Abschlussprüfung",
            "Alle Einstellungen werden noch einmal überprüft."),
        new(SetupStep.ClientConfiguration, "Client-Konfiguration",
            "Die Datei für die Arbeitsplätze wird erzeugt.")
    ];

    /// <summary>
    /// Dieselben zwoelf Schritte, aber in der Sprache des Solo-Platzes: dort
    /// gibt es keinen Server, keine Instanz und keine Freigaben. Die Nummern
    /// bleiben gleich, damit Anleitung und Assistent zusammenpassen.
    /// </summary>
    public static IReadOnlyList<SetupStepInfo> AllForSingleWorkstation { get; } =
    [
        .. All.Select(schritt => schritt.Step switch
        {
            SetupStep.DetectSqlServer => schritt with
            {
                Title = "Datenbankserver",
                Description = "Beim Solo-Platz nicht nötig – es wird nichts nachinstalliert."
            },
            SetupStep.TestConnection => schritt with
            {
                Title = "Datenbankdatei",
                Description = "Der Ort der Datenbankdatei wird festgelegt und geprüft."
            },
            SetupStep.NetworkShares => schritt with
            {
                Title = "Netzwerkfreigaben",
                Description = "Beim Solo-Platz nicht nötig – alle Ordner liegen hier."
            },
            SetupStep.ClientConfiguration => schritt with
            {
                Title = "Abschluss",
                Description = "Beim Solo-Platz wird keine Datei für weitere Arbeitsplätze gebraucht."
            },
            _ => schritt
        })
    ];
}

/// <summary>Ergebnis einer Pruefung im Assistenten.</summary>
public sealed record SetupCheckResult(bool IsSuccessful, string Message, string? Hint = null);
