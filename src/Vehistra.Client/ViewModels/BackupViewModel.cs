using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Client.Services;

namespace Vehistra.Client.ViewModels;

/// <summary>Verwaltung der Datenbanksicherungen.</summary>
public sealed partial class BackupViewModel : ViewModelBase
{
    private readonly IBackupService _backup;
    private readonly ISettingsService _settings;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private BackupInfo? _selectedBackup;

    [ObservableProperty]
    private string? _backupDirectory;

    [ObservableProperty]
    private string? _lastSuccessful;

    [ObservableProperty]
    private string? _progressText;

    public BackupViewModel(
        IBackupService backup,
        ISettingsService settings,
        IConnectionSettingsStore connectionStore,
        IDialogService dialogs)
    {
        _backup = backup;
        _settings = settings;
        _connectionStore = connectionStore;
        _dialogs = dialogs;
    }

    public override string Title => "Backups";

    public override string? Subtitle =>
        LastSuccessful is null
            ? "Noch keine erfolgreiche Sicherung protokolliert"
            : $"Letzte erfolgreiche Sicherung: {LastSuccessful}";

    public ObservableCollection<BackupInfo> History { get; } = [];

    /// <summary>Empfohlenes Aufbewahrungsschema.</summary>
    public string RetentionRecommendation =>
        "Empfohlen: 7 tägliche, 4 wöchentliche und 12 monatliche Sicherungen aufbewahren.";

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            BackupDirectory = _connectionStore.Load()?.BackupPath
                ?? await _settings.GetAsync(SettingsKeys.BackupPath, cancellationToken).ConfigureAwait(true);

            var history = await _backup.GetHistoryAsync(100, cancellationToken).ConfigureAwait(true);

            History.Clear();
            foreach (var entry in history)
            {
                History.Add(entry);
            }

            var last = history.FirstOrDefault(h => h.IsSuccessful);
            LastSuccessful = last is null
                ? null
                : $"{last.StartedAt:dd.MM.yyyy HH:mm} ({last.Kind})";

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (string.IsNullOrWhiteSpace(BackupDirectory))
        {
            _dialogs.ShowWarning(
                "Es ist kein Backupverzeichnis hinterlegt. Bitte tragen Sie es in den Servereinstellungen ein." +
                Environment.NewLine + Environment.NewLine +
                "Der Pfad muss aus Sicht des SQL Servers gültig sein, z. B. D:\\Fuhrpark\\Backups.",
                "Backupverzeichnis fehlt");
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll jetzt eine Sicherung der Fuhrparkdatenbank erstellt werden?{Environment.NewLine}{Environment.NewLine}" +
                $"Zielverzeichnis: {BackupDirectory}",
                "Backup erstellen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var progress = new Progress<string>(text => ProgressText = text);

            var result = await _backup.CreateBackupAsync(new BackupRequest
            {
                Directory = BackupDirectory,
                Kind = "Manuell",
                VerifyAfterBackup = true
            }, progress).ConfigureAwait(true);

            ProgressText = null;

            if (result.IsSuccessful)
            {
                _dialogs.ShowInformation(
                    result.Message + Environment.NewLine + Environment.NewLine + result.FilePath,
                    "Sicherung erstellt");
            }
            else
            {
                _dialogs.ShowError(result.Message, null, "Sicherung fehlgeschlagen");
            }

            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        if (SelectedBackup?.FilePath is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _backup.VerifyBackupAsync(SelectedBackup.FilePath).ConfigureAwait(true);

            if (result.IsSuccessful)
            {
                _dialogs.ShowInformation(result.Message, "Prüfung erfolgreich");
            }
            else
            {
                _dialogs.ShowError(result.Message, null, "Prüfung fehlgeschlagen");
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ShowRestoreInstructions()
    {
        var settings = _connectionStore.Load();

        // Beim Solo-Platz gibt es kein Management Studio und keinen Server -
        // dort ist die Datenbank eine Datei, und genau das muss hier stehen.
        if (settings?.IsSingleWorkstation == true)
        {
            ShowSoloRestoreInstructions(settings);
            return;
        }

        var database = settings?.Database ?? "VehistraDB";
        var path = SelectedBackup?.FilePath ?? @"D:\Fuhrpark\Backups\VehistraDB_JJJJMMTT.bak";

        _dialogs.ShowInformation(
            $"""
             WIEDERHERSTELLUNG DER DATENBANK

             1. Alle Arbeitsplätze beenden das Vehistra.
             2. Auf dem Server das SQL Server Management Studio als Administrator starten.
             3. Neue Abfrage öffnen und ausführen:

                USE master;
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [{database}] FROM DISK = N'{path}' WITH REPLACE;
                ALTER DATABASE [{database}] SET MULTI_USER;

             4. Anschließend die Arbeitsplätze wieder starten.

             Die ausführliche Anleitung finden Sie in BACKUP-UND-WIEDERHERSTELLUNG.pdf.
             """,
            "Wiederherstellung");
    }

    /// <summary>Wiederherstellung des Solo-Platzes: eine Datei wird ersetzt.</summary>
    private void ShowSoloRestoreInstructions(ServerConnectionSettings settings)
    {
        var databaseFile = string.IsNullOrWhiteSpace(settings.DatabaseFile)
            ? "Vehistra.db"
            : settings.DatabaseFile;

        var folder = Path.GetDirectoryName(databaseFile);
        var path = SelectedBackup?.FilePath ?? Path.Combine(
            string.IsNullOrWhiteSpace(BackupDirectory) ? @"C:\Vehistra\Backups" : BackupDirectory,
            "Vehistra_JJJJMMTT_HHMMSS_Manuell.db");

        _dialogs.ShowInformation(
            $"""
             WIEDERHERSTELLUNG DER DATENBANK (SOLO-PLATZ)

             1. Vehistra schließen.
             2. Diesen Ordner öffnen:

                {folder}

             3. Die Dateien Vehistra.db, Vehistra.db-wal und Vehistra.db-shm in
                einen neuen Unterordner "Alt" verschieben - nicht löschen.
             4. Die Sicherung dorthin kopieren und in Vehistra.db umbenennen:

                {path}

             5. Vehistra starten und Stichproben prüfen (Fahrzeugliste,
                letzte Kilometerstände, letzte Wartung).
             6. Erst danach den Ordner "Alt" löschen.

             Die ausführliche Anleitung finden Sie in
             BACKUP-UND-WIEDERHERSTELLUNG.pdf, Kapitel 9.
             """,
            "Wiederherstellung");
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        if (!string.IsNullOrWhiteSpace(BackupDirectory))
        {
            _dialogs.OpenInShell(BackupDirectory);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
