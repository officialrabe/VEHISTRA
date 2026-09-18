using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Security.Principal;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Persistence.Seeding;
using Vehistra.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vehistra.ServerSetup;

/// <summary>
/// Ansichtsmodell des Einrichtungsassistenten. Fuehrt Schritt fuer Schritt durch die
/// Servereinrichtung und erklaert jeden Schritt in einfacher Sprache.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class SetupViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly ILogger<SetupViewModel> _logger;

    private string _sqlPassword = string.Empty;
    private string _administratorPassword = string.Empty;

    [ObservableProperty]
    private SetupStep _currentStep = SetupStep.SystemCheck;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    // Schritt 2 und 3
    [ObservableProperty]
    private SqlInstanceInfo? _selectedInstance;

    [ObservableProperty]
    private string _server = Environment.MachineName + "\\SQLEXPRESS";

    [ObservableProperty]
    private string _database = "VehistraDB";

    [ObservableProperty]
    private bool _useWindowsAuthentication = true;

    [ObservableProperty]
    private string? _sqlUserName;

    [ObservableProperty]
    private bool _connectionTested;

    // Schritt 6 bis 8
    [ObservableProperty]
    private string _documentsPath = @"D:\Fuhrpark\Dokumente";

    [ObservableProperty]
    private string _backupPath = @"D:\Fuhrpark\Backups";

    [ObservableProperty]
    private string _updatePath = @"D:\Fuhrpark\Updates";

    [ObservableProperty]
    private string? _documentsShare;

    [ObservableProperty]
    private string? _updateShare;

    // Schritt 10
    [ObservableProperty]
    private string _adminUserName = "admin";

    [ObservableProperty]
    private string _adminFirstName = string.Empty;

    [ObservableProperty]
    private string _adminLastName = "Administrator";

    [ObservableProperty]
    private string? _adminEmail;

    [ObservableProperty]
    private bool _administratorCreated;

    // Schritt 12
    [ObservableProperty]
    private string? _configurationPath;

    public SetupViewModel(
        IServiceProvider services,
        IConnectionSettingsStore connectionStore,
        ILogger<SetupViewModel> logger)
    {
        _services = services;
        _connectionStore = connectionStore;
        _logger = logger;
    }

    public IReadOnlyList<SetupStepInfo> Steps { get; } = SetupStepInfo.All;

    public ObservableCollection<SqlInstanceInfo> Instances { get; } = [];

    public ObservableCollection<SetupCheckResult> Checks { get; } = [];

    public ObservableCollection<string> Log { get; } = [];

    public bool UseSqlAuthentication => !UseWindowsAuthentication;

    public SetupStepInfo CurrentStepInfo => Steps.First(s => s.Step == CurrentStep);

    public bool CanGoBack => CurrentStep > SetupStep.SystemCheck;

    public bool IsLastStep => CurrentStep == SetupStep.ClientConfiguration;

    /// <summary>Fortschrittsanzeige in Worten, z. B. "Schritt 3 von 12".</summary>
    public string StepProgress => $"Schritt {(int)CurrentStep} von {Steps.Count}";

    /// <summary>Beschriftung der Aktionsschaltflaeche des aktuellen Schritts.</summary>
    public string ExecuteCaption => CurrentStep switch
    {
        SetupStep.SystemCheck => "System prüfen",
        SetupStep.DetectSqlServer => "SQL Server suchen",
        SetupStep.TestConnection => "Verbindung testen",
        SetupStep.CreateDatabase => "Datenbank anlegen",
        SetupStep.ApplyMigrations => "Struktur einrichten",
        SetupStep.DocumentsFolder => "Ordner anlegen",
        SetupStep.BackupFolder => "Ordner anlegen",
        SetupStep.UpdateFolder => "Ordner anlegen",
        SetupStep.NetworkShares => "Freigaben prüfen",
        SetupStep.Administrator => "Konto anlegen",
        SetupStep.FinalCheck => "Abschluss prüfen",
        SetupStep.ClientConfiguration => "Konfiguration speichern",
        _ => "Ausführen"
    };

    public void SetSqlPassword(string password) => _sqlPassword = password;

    public void SetAdministratorPassword(string password) => _administratorPassword = password;

    partial void OnCurrentStepChanged(SetupStep value)
    {
        OnPropertyChanged(nameof(CurrentStepInfo));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(StepProgress));
        OnPropertyChanged(nameof(ExecuteCaption));
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseWindowsAuthenticationChanged(bool value) => OnPropertyChanged(nameof(UseSqlAuthentication));

    partial void OnSelectedInstanceChanged(SqlInstanceInfo? value)
    {
        if (value is not null)
        {
            Server = value.DataSource;
        }
    }

    [RelayCommand]
    private async Task ExecuteStepAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            switch (CurrentStep)
            {
                case SetupStep.SystemCheck:
                    RunSystemCheck();
                    break;

                case SetupStep.DetectSqlServer:
                    DetectInstances();
                    break;

                case SetupStep.TestConnection:
                    await TestConnectionAsync().ConfigureAwait(true);
                    break;

                case SetupStep.CreateDatabase:
                    await CreateDatabaseAsync().ConfigureAwait(true);
                    break;

                case SetupStep.ApplyMigrations:
                    await ApplyMigrationsAsync().ConfigureAwait(true);
                    break;

                case SetupStep.DocumentsFolder:
                    CreateFolder(DocumentsPath, "Dokumentenordner");
                    break;

                case SetupStep.BackupFolder:
                    CreateFolder(BackupPath, "Backupordner");
                    break;

                case SetupStep.UpdateFolder:
                    CreateUpdateFolder();
                    break;

                case SetupStep.NetworkShares:
                    CheckShares();
                    break;

                case SetupStep.Administrator:
                    await CreateAdministratorAsync().ConfigureAwait(true);
                    break;

                case SetupStep.FinalCheck:
                    await RunFinalCheckAsync().ConfigureAwait(true);
                    break;

                case SetupStep.ClientConfiguration:
                    ExportConfiguration();
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Schritt {Step} ist fehlgeschlagen.", CurrentStep);
            ErrorMessage = exception.Message;
            AddLog($"FEHLER: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep < SetupStep.ClientConfiguration)
        {
            CurrentStep++;
            Checks.Clear();
            StatusMessage = null;
            ErrorMessage = null;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > SetupStep.SystemCheck)
        {
            CurrentStep--;
            Checks.Clear();
            StatusMessage = null;
            ErrorMessage = null;
        }
    }

    private void RunSystemCheck()
    {
        Checks.Clear();

        var isWindows = OperatingSystem.IsWindows();
        Checks.Add(new SetupCheckResult(isWindows,
            isWindows ? $"Betriebssystem: {Environment.OSVersion}" : "Diese Einrichtung setzt Windows voraus.",
            isWindows ? null : "Bitte führen Sie die Einrichtung auf dem Windows Server aus."));

        var isAdministrator = IsRunningAsAdministrator();
        Checks.Add(new SetupCheckResult(isAdministrator,
            isAdministrator
                ? "Das Programm läuft mit Administratorrechten."
                : "Das Programm läuft ohne Administratorrechte.",
            isAdministrator
                ? null
                : "Bitte rechts auf VehistraServerSetup.exe klicken und „Als Administrator ausführen“ wählen."));

        Checks.Add(new SetupCheckResult(true, $"Computername: {Environment.MachineName}"));
        Checks.Add(new SetupCheckResult(true, $".NET-Laufzeit: {Environment.Version}"));

        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => $"{d.Name} {d.AvailableFreeSpace / 1024 / 1024 / 1024} GB frei");

        Checks.Add(new SetupCheckResult(true, "Laufwerke: " + string.Join(" · ", drives)));

        StatusMessage = isAdministrator
            ? "Die Systemprüfung war erfolgreich."
            : "Bitte starten Sie das Programm als Administrator neu.";

        AddLog("Systemprüfung abgeschlossen.");
    }

    private void DetectInstances()
    {
        Instances.Clear();
        Checks.Clear();

        foreach (var instance in SqlInstanceLocator.FindLocalInstances())
        {
            Instances.Add(instance);
        }

        if (Instances.Count == 0)
        {
            Checks.Add(new SetupCheckResult(false,
                "Microsoft SQL Server Express wurde nicht gefunden.",
                "Bitte installieren Sie zuerst SQL Server Express (Instanzname SQLEXPRESS). " +
                "Die Anleitung finden Sie in SERVER-EINRICHTUNG-EINFACH.pdf, Kapitel 2. " +
                "Klicken Sie danach auf „Erneut prüfen“."));

            StatusMessage = "Es wurde keine SQL-Server-Instanz gefunden.";
            AddLog("Keine SQL-Server-Instanz gefunden.");
            return;
        }

        SelectedInstance = Instances.FirstOrDefault(i =>
            i.InstanceName.Equals("SQLEXPRESS", StringComparison.OrdinalIgnoreCase)) ?? Instances[0];

        foreach (var instance in Instances)
        {
            Checks.Add(new SetupCheckResult(true, instance.Display));
        }

        StatusMessage = $"{Instances.Count} SQL-Server-Instanz(en) gefunden.";
        AddLog($"{Instances.Count} SQL-Server-Instanz(en) gefunden.");
    }

    private async Task TestConnectionAsync()
    {
        Checks.Clear();

        using var scope = _services.CreateScope();
        var administration = scope.ServiceProvider.GetRequiredService<IDatabaseAdministrationService>();

        var settings = BuildSettings();
        var result = await administration.TestConnectionAsync(settings).ConfigureAwait(true);

        Checks.Add(new SetupCheckResult(result.IsSuccessful, result.Message,
            result.Hints is null ? null : string.Join(Environment.NewLine, result.Hints.Select(h => "• " + h))));

        if (result.IsSuccessful && result.TechnicalDetails is not null)
        {
            Checks.Add(new SetupCheckResult(true, result.TechnicalDetails));
        }

        ConnectionTested = result.IsSuccessful;
        StatusMessage = result.Message;
        AddLog(result.Message);

        if (result.IsSuccessful)
        {
            // Die Verbindung sofort speichern, damit die folgenden Schritte sie verwenden.
            _connectionStore.Save(settings);
        }
    }

    private async Task CreateDatabaseAsync()
    {
        Checks.Clear();

        using var scope = _services.CreateScope();
        var administration = scope.ServiceProvider.GetRequiredService<IDatabaseAdministrationService>();

        var settings = BuildSettings();

        if (await administration.DatabaseExistsAsync(settings).ConfigureAwait(true))
        {
            Checks.Add(new SetupCheckResult(true,
                $"Die Datenbank „{settings.Database}“ ist bereits vorhanden und wird weiterverwendet.",
                "Vorhandene Daten bleiben vollständig erhalten."));

            StatusMessage = "Bestehende Datenbank erkannt.";
            AddLog($"Bestehende Datenbank {settings.Database} erkannt.");
            return;
        }

        await administration.CreateDatabaseAsync(settings).ConfigureAwait(true);

        Checks.Add(new SetupCheckResult(true, $"Die Datenbank „{settings.Database}“ wurde angelegt."));
        StatusMessage = "Die Datenbank wurde angelegt.";
        AddLog($"Datenbank {settings.Database} angelegt.");
    }

    private async Task ApplyMigrationsAsync()
    {
        Checks.Clear();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehistraDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        var pending = (await db.Database.GetPendingMigrationsAsync().ConfigureAwait(true)).ToList();

        if (pending.Count > 0)
        {
            AddLog($"{pending.Count} Datenbankänderung(en) werden angewendet ...");
            await db.Database.MigrateAsync().ConfigureAwait(true);
            Checks.Add(new SetupCheckResult(true, $"{pending.Count} Datenbankänderung(en) angewendet."));
        }
        else
        {
            Checks.Add(new SetupCheckResult(true, "Die Datenbankstruktur ist bereits aktuell."));
        }

        await seeder.SeedSystemDataAsync().ConfigureAwait(true);
        Checks.Add(new SetupCheckResult(true,
            "Stammdaten eingerichtet: Rollen, Berechtigungen, Fahrzeugstatus, Kategorien und Einstellungen."));

        var tableCount = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables")
            .FirstOrDefaultAsync()
            .ConfigureAwait(true);

        Checks.Add(new SetupCheckResult(true, $"{tableCount} Tabellen in der Datenbank vorhanden."));

        StatusMessage = "Die Datenbankstruktur wurde eingerichtet.";
        AddLog("Datenbankstruktur eingerichtet.");
    }

    private void CreateFolder(string path, string description)
    {
        Checks.Clear();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"Bitte geben Sie einen Pfad für den {description} an.");
        }

        Directory.CreateDirectory(path);
        Checks.Add(new SetupCheckResult(true, $"{description} angelegt bzw. vorhanden: {path}"));

        var probe = Path.Combine(path, $".schreibtest_{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probe, "Schreibtest");
            File.Delete(probe);
            Checks.Add(new SetupCheckResult(true, "Schreibrechte vorhanden."));
        }
        catch (Exception exception)
        {
            Checks.Add(new SetupCheckResult(false, $"Es kann nicht geschrieben werden: {exception.Message}",
                "Bitte NTFS-Berechtigungen des Ordners prüfen (Kapitel 12 der Serveranleitung)."));
        }

        StatusMessage = $"{description} eingerichtet.";
        AddLog($"{description}: {path}");
    }

    private void CreateUpdateFolder()
    {
        CreateFolder(UpdatePath, "Updateordner");

        // Die vom Updatesystem erwartete Struktur vorbereiten.
        Directory.CreateDirectory(Path.Combine(UpdatePath, "Archive"));

        var manifest = Path.Combine(UpdatePath, "latest.json");

        if (!File.Exists(manifest))
        {
            File.WriteAllText(manifest,
                """
                {
                  "version": "1.0.0",
                  "minimumVersion": "1.0.0",
                  "installer": "",
                  "releaseNotes": "",
                  "mandatory": false
                }
                """);

            Checks.Add(new SetupCheckResult(true,
                "Die Datei latest.json wurde vorbereitet.",
                "Neue Versionen werden später in Unterordner (z. B. 1.1.0) gelegt und hier eingetragen."));
        }
    }

    private void CheckShares()
    {
        Checks.Clear();

        CheckShare(DocumentsShare, "Dokumentenfreigabe");
        CheckShare(UpdateShare, "Updatefreigabe");

        if (string.IsNullOrWhiteSpace(DocumentsShare) && string.IsNullOrWhiteSpace(UpdateShare))
        {
            Checks.Add(new SetupCheckResult(true,
                "Es wurden keine UNC-Pfade angegeben.",
                "Für den Mehrplatzbetrieb sollten die Ordner als Netzwerkfreigabe bereitgestellt werden " +
                "(Kapitel 11 der Serveranleitung)."));
        }

        StatusMessage = "Die Freigaben wurden geprüft.";
    }

    private void CheckShare(string? share, string description)
    {
        if (string.IsNullOrWhiteSpace(share))
        {
            return;
        }

        if (!Directory.Exists(share))
        {
            Checks.Add(new SetupCheckResult(false, $"{description} nicht erreichbar: {share}",
                "Bitte Freigabename und Berechtigungen prüfen."));
            return;
        }

        Checks.Add(new SetupCheckResult(true, $"{description} erreichbar: {share}"));

        var probe = Path.Combine(share, $".schreibtest_{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probe, "Schreibtest");
            File.Delete(probe);
            Checks.Add(new SetupCheckResult(true, $"{description}: Schreibrechte vorhanden."));
        }
        catch (Exception exception)
        {
            Checks.Add(new SetupCheckResult(false, $"{description}: kein Schreibzugriff ({exception.Message})",
                "Bitte Freigabe- und NTFS-Berechtigungen prüfen."));
        }
    }

    private async Task CreateAdministratorAsync()
    {
        Checks.Clear();

        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        var authentication = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

        if (await authentication.HasAnyActiveAdministratorAsync().ConfigureAwait(true))
        {
            Checks.Add(new SetupCheckResult(true,
                "Es existiert bereits ein aktives Administratorkonto.",
                "Dieser Schritt kann übersprungen werden."));

            AdministratorCreated = true;
            StatusMessage = "Administratorkonto bereits vorhanden.";
            return;
        }

        var errors = await authentication.ValidatePasswordAsync(_administratorPassword).ConfigureAwait(true);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        await seeder.CreateAdministratorAsync(
            AdminUserName.Trim(), _administratorPassword, AdminFirstName.Trim(), AdminLastName.Trim(), AdminEmail)
            .ConfigureAwait(true);

        AdministratorCreated = true;
        _administratorPassword = string.Empty;

        Checks.Add(new SetupCheckResult(true, $"Das Administratorkonto „{AdminUserName}“ wurde angelegt."));
        StatusMessage = "Das Administratorkonto wurde angelegt.";
        AddLog($"Administratorkonto {AdminUserName} angelegt.");
    }

    private async Task RunFinalCheckAsync()
    {
        Checks.Clear();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehistraDbContext>();
        var authentication = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

        var canConnect = await db.Database.CanConnectAsync().ConfigureAwait(true);
        Checks.Add(new SetupCheckResult(canConnect,
            canConnect ? "Die Datenbank ist erreichbar." : "Die Datenbank ist nicht erreichbar."));

        var pending = (await db.Database.GetPendingMigrationsAsync().ConfigureAwait(true)).ToList();
        Checks.Add(new SetupCheckResult(pending.Count == 0,
            pending.Count == 0
                ? "Die Datenbankstruktur ist aktuell."
                : $"Es stehen noch {pending.Count} Datenbankänderung(en) aus."));

        var hasAdministrator = await authentication.HasAnyActiveAdministratorAsync().ConfigureAwait(true);
        Checks.Add(new SetupCheckResult(hasAdministrator,
            hasAdministrator
                ? "Es existiert ein aktives Administratorkonto."
                : "Es wurde noch kein Administratorkonto angelegt."));

        Checks.Add(new SetupCheckResult(Directory.Exists(DocumentsPath),
            Directory.Exists(DocumentsPath)
                ? $"Dokumentenordner vorhanden: {DocumentsPath}"
                : $"Dokumentenordner fehlt: {DocumentsPath}"));

        Checks.Add(new SetupCheckResult(Directory.Exists(BackupPath),
            Directory.Exists(BackupPath)
                ? $"Backupordner vorhanden: {BackupPath}"
                : $"Backupordner fehlt: {BackupPath}"));

        Checks.Add(new SetupCheckResult(Directory.Exists(UpdatePath),
            Directory.Exists(UpdatePath)
                ? $"Updateordner vorhanden: {UpdatePath}"
                : $"Updateordner fehlt: {UpdatePath}"));

        var settings = BuildSettings();
        settings.DocumentsPath = string.IsNullOrWhiteSpace(DocumentsShare) ? DocumentsPath : DocumentsShare;
        settings.UpdatePath = string.IsNullOrWhiteSpace(UpdateShare) ? UpdatePath : UpdateShare;
        settings.BackupPath = BackupPath;
        _connectionStore.Save(settings);

        // Pfade zusaetzlich in den Systemeinstellungen hinterlegen.
        var db2 = scope.ServiceProvider.GetRequiredService<VehistraDbContext>();
        await UpsertSettingAsync(db2, SettingsKeys.DocumentsPath, settings.DocumentsPath).ConfigureAwait(true);
        await UpsertSettingAsync(db2, SettingsKeys.BackupPath, settings.BackupPath).ConfigureAwait(true);
        await UpsertSettingAsync(db2, SettingsKeys.UpdatePath, settings.UpdatePath).ConfigureAwait(true);

        StatusMessage = Checks.All(c => c.IsSuccessful)
            ? "Die Einrichtung ist vollständig."
            : "Es sind noch Punkte offen – bitte die Hinweise beachten.";

        AddLog("Abschlussprüfung durchgeführt.");
    }

    private static async Task UpsertSettingAsync(VehistraDbContext db, string key, string? value)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key).ConfigureAwait(true);

        if (setting is null)
        {
            db.SystemSettings.Add(new Domain.Entities.SystemSetting
            {
                Key = key,
                Value = value,
                Category = "Pfade",
                DataType = "path",
                IsSystemSetting = true
            });
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync().ConfigureAwait(true);
    }

    private void ExportConfiguration()
    {
        Checks.Clear();

        var settings = BuildSettings();
        settings.DocumentsPath = string.IsNullOrWhiteSpace(DocumentsShare) ? DocumentsPath : DocumentsShare;
        settings.UpdatePath = string.IsNullOrWhiteSpace(UpdateShare) ? UpdatePath : UpdateShare;
        settings.BackupPath = BackupPath;

        var target = string.IsNullOrWhiteSpace(ConfigurationPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Vehistra-Firmenkonfiguration.fmcfg")
            : ConfigurationPath;

        _connectionStore.ExportClientConfiguration(settings, target);
        ConfigurationPath = target;

        Checks.Add(new SetupCheckResult(true, $"Die Firmenkonfiguration wurde gespeichert: {target}",
            "Diese Datei enthält keine Passwörter. Sie wird beim Einrichten neuer Arbeitsplätze ausgewählt."));

        StatusMessage = "Die Einrichtung ist abgeschlossen.";
        AddLog($"Firmenkonfiguration exportiert: {target}");
    }

    private ServerConnectionSettings BuildSettings()
    {
        var existing = _connectionStore.Load();

        return new ServerConnectionSettings
        {
            Server = Server.Trim(),
            Database = Database.Trim(),
            UseWindowsAuthentication = UseWindowsAuthentication,
            SqlUserName = UseWindowsAuthentication ? null : SqlUserName?.Trim(),
            ProtectedSqlPassword = UseWindowsAuthentication
                ? null
                : string.IsNullOrEmpty(_sqlPassword)
                    ? existing?.ProtectedSqlPassword
                    : SecretProtector.Protect(_sqlPassword),
            DocumentsPath = string.IsNullOrWhiteSpace(DocumentsShare) ? DocumentsPath : DocumentsShare,
            UpdatePath = string.IsNullOrWhiteSpace(UpdateShare) ? UpdatePath : UpdateShare,
            BackupPath = BackupPath,
            ConfiguredAt = DateTime.Now,
            ConfiguredBy = Environment.UserName
        };
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void AddLog(string message) => Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
}
