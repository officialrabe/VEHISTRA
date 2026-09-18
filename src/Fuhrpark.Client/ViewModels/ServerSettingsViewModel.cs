using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Client.Services;
using Fuhrpark.Infrastructure.Security;
using Fuhrpark.Infrastructure.Storage;

namespace Fuhrpark.Client.ViewModels;

/// <summary>
/// Einrichtung der Serververbindung. Windows-Authentifizierung wird bevorzugt;
/// ein SQL-Passwort wird ausschliesslich DPAPI-verschluesselt gespeichert.
/// </summary>
public sealed partial class ServerSettingsViewModel : ViewModelBase
{
    private readonly IConnectionSettingsStore _store;
    private readonly IDatabaseAdministrationService _databaseAdministration;
    private readonly FileSystemDocumentStorage _storage;
    private readonly IDialogService _dialogs;

    private string _sqlPassword = string.Empty;

    [ObservableProperty]
    private string _server = string.Empty;

    [ObservableProperty]
    private string _database = "FuhrparkDB";

    [ObservableProperty]
    private bool _useWindowsAuthentication = true;

    [ObservableProperty]
    private string? _sqlUserName;

    [ObservableProperty]
    private string? _documentsPath;

    [ObservableProperty]
    private string? _updatePath;

    [ObservableProperty]
    private string? _backupPath;

    [ObservableProperty]
    private string? _testResult;

    [ObservableProperty]
    private bool _testSuccessful;

    [ObservableProperty]
    private string? _testHints;

    public ServerSettingsViewModel(
        IConnectionSettingsStore store,
        IDatabaseAdministrationService databaseAdministration,
        FileSystemDocumentStorage storage,
        IDialogService dialogs)
    {
        _store = store;
        _databaseAdministration = databaseAdministration;
        _storage = storage;
        _dialogs = dialogs;
    }

    public event EventHandler<bool>? CloseRequested;

    public bool UseSqlAuthentication => !UseWindowsAuthentication;

    public override string Title => "Servereinstellungen";

    public override Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = _store.Load();

        if (settings is not null)
        {
            Server = settings.Server;
            Database = settings.Database;
            UseWindowsAuthentication = settings.UseWindowsAuthentication;
            SqlUserName = settings.SqlUserName;
            DocumentsPath = settings.DocumentsPath;
            UpdatePath = settings.UpdatePath;
            BackupPath = settings.BackupPath;
        }

        return Task.CompletedTask;
    }

    public void SetSqlPassword(string password) => _sqlPassword = password;

    partial void OnUseWindowsAuthenticationChanged(bool value) => OnPropertyChanged(nameof(UseSqlAuthentication));

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        TestResult = null;
        TestHints = null;

        await RunAsync(async () =>
        {
            var settings = BuildSettings();
            var result = await _databaseAdministration.TestConnectionAsync(settings).ConfigureAwait(true);

            TestSuccessful = result.IsSuccessful;
            TestResult = result.Message;
            TestHints = result.Hints is null || result.Hints.Count == 0
                ? null
                : string.Join(Environment.NewLine, result.Hints.Select(h => "• " + h));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Server))
            {
                ErrorMessage = "Bitte geben Sie den Servernamen an, z. B. FUHRPARK-SRV01\\SQLEXPRESS.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Database))
            {
                ErrorMessage = "Bitte geben Sie den Namen der Datenbank an, z. B. FuhrparkDB.";
                return;
            }

            if (UseSqlAuthentication && string.IsNullOrWhiteSpace(SqlUserName))
            {
                ErrorMessage = "Bitte geben Sie den SQL-Benutzernamen an oder verwenden Sie die Windows-Authentifizierung.";
                return;
            }

            var settings = BuildSettings();
            var result = await _databaseAdministration.TestConnectionAsync(settings).ConfigureAwait(true);

            if (!result.IsSuccessful)
            {
                var hints = result.Hints is null
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine +
                      string.Join(Environment.NewLine, result.Hints.Select(h => "• " + h));

                if (!_dialogs.Confirm(
                        result.Message + hints + Environment.NewLine + Environment.NewLine +
                        "Möchten Sie die Einstellungen trotzdem speichern?",
                        "Verbindung nicht möglich"))
                {
                    return;
                }
            }

            settings.ConfiguredAt = DateTime.Now;
            settings.ConfiguredBy = Environment.UserName;

            _store.Save(settings);
            _storage.Configure(settings.DocumentsPath);

            CloseRequested?.Invoke(this, true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ImportConfiguration()
    {
        var path = _dialogs.OpenFile(
            "Firmenkonfiguration (*.fmcfg)|*.fmcfg|Alle Dateien (*.*)|*.*",
            "Firmenkonfiguration auswählen");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var imported = _store.ImportClientConfiguration(path);

            Server = imported.Server;
            Database = imported.Database;
            UseWindowsAuthentication = imported.UseWindowsAuthentication;
            SqlUserName = imported.SqlUserName;
            DocumentsPath = imported.DocumentsPath;
            UpdatePath = imported.UpdatePath;

            StatusMessage = "Die Firmenkonfiguration wurde übernommen. Bitte die Verbindung testen und speichern.";
        }
        catch (Exception exception)
        {
            ErrorMessage = Describe(exception);
        }
    }

    [RelayCommand]
    private void ExportConfiguration()
    {
        var target = _dialogs.SaveFile(
            "Firmenkonfiguration (*.fmcfg)|*.fmcfg",
            "Fuhrpark-Firmenkonfiguration.fmcfg",
            "Firmenkonfiguration exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        try
        {
            _store.ExportClientConfiguration(BuildSettings(), target);
            _dialogs.ShowInformation(
                "Die Firmenkonfiguration wurde gespeichert." + Environment.NewLine +
                "Sie enthält bewusst keine Passwörter.",
                "Export abgeschlossen");
        }
        catch (Exception exception)
        {
            ErrorMessage = Describe(exception);
        }
    }

    [RelayCommand]
    private void BrowseDocuments() => DocumentsPath = _dialogs.SelectFolder("Dokumentenablage auswählen") ?? DocumentsPath;

    [RelayCommand]
    private void BrowseUpdates() => UpdatePath = _dialogs.SelectFolder("Updateablage auswählen") ?? UpdatePath;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    private ServerConnectionSettings BuildSettings()
    {
        var existing = _store.Load();

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
            DocumentsPath = string.IsNullOrWhiteSpace(DocumentsPath) ? null : DocumentsPath.Trim(),
            UpdatePath = string.IsNullOrWhiteSpace(UpdatePath) ? null : UpdatePath.Trim(),
            BackupPath = string.IsNullOrWhiteSpace(BackupPath) ? null : BackupPath.Trim(),
            ConfiguredAt = existing?.ConfiguredAt,
            ConfiguredBy = existing?.ConfiguredBy
        };
    }
}
