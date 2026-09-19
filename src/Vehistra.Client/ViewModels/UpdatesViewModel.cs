using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Client.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Client.ViewModels;

/// <summary>
/// Updateverwaltung: Pruefung der zentralen Updateablage, Anzeige der Release Notes,
/// Start des Updaters und Ausfuehrung ausstehender Datenbankmigrationen.
/// </summary>
public sealed partial class UpdatesViewModel : ViewModelBase
{
    private readonly IUpdateService _updates;
    private readonly IDatabaseAdministrationService _databaseAdministration;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly ICurrentUserService _currentUser;
    private readonly VehistraDbContext _db;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string _installedVersion = "1.0.0";

    [ObservableProperty]
    private string? _availableVersion;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isMandatory;

    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private string? _updatePath;

    [ObservableProperty]
    private string? _checkMessage;

    [ObservableProperty]
    private string? _databaseSchemaVersion;

    [ObservableProperty]
    private int _pendingMigrations;

    [ObservableProperty]
    private string? _migrationProgress;

    /// <summary>Ergebnis der Pruefsummenpruefung des gefundenen Pakets.</summary>
    [ObservableProperty]
    private string? _checksumMessage;

    [ObservableProperty]
    private bool _isChecksumValid;

    private UpdateCheckResult? _lastCheck;

    public UpdatesViewModel(
        IUpdateService updates,
        IDatabaseAdministrationService databaseAdministration,
        IConnectionSettingsStore connectionStore,
        ICurrentUserService currentUser,
        VehistraDbContext db,
        IDialogService dialogs)
    {
        _updates = updates;
        _databaseAdministration = databaseAdministration;
        _connectionStore = connectionStore;
        _currentUser = currentUser;
        _db = db;
        _dialogs = dialogs;
    }

    public override string Title => "Updates";

    public override string? Subtitle =>
        IsUpdateAvailable
            ? $"Installiert {InstalledVersion} · verfügbar {AvailableVersion}"
            : $"Installiert {InstalledVersion} · keine neue Version verfügbar";

    public ObservableCollection<UpdateHistory> History { get; } = [];

    public ObservableCollection<DatabaseVersion> DatabaseVersions { get; } = [];

    public bool CanInstall => _currentUser.HasPermission(Permissions.UpdatesManage);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            InstalledVersion = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            UpdatePath = _connectionStore.Load()?.UpdatePath;

            var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(true);
            PendingMigrations = pending.Count();

            var current = await _db.DatabaseVersions
                .AsNoTracking()
                .Where(v => v.IsCurrent)
                .OrderByDescending(v => v.AppliedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(true);

            DatabaseSchemaVersion = current?.SchemaVersion ?? "unbekannt";

            History.Clear();
            foreach (var entry in await _db.UpdateHistory
                         .AsNoTracking()
                         .OrderByDescending(h => h.StartedAt)
                         .Take(50)
                         .ToListAsync(cancellationToken)
                         .ConfigureAwait(true))
            {
                History.Add(entry);
            }

            DatabaseVersions.Clear();
            foreach (var version in await _db.DatabaseVersions
                         .AsNoTracking()
                         .OrderByDescending(v => v.AppliedAt)
                         .Take(50)
                         .ToListAsync(cancellationToken)
                         .ConfigureAwait(true))
            {
                DatabaseVersions.Add(version);
            }

            await CheckAsync(cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await _updates.CheckForUpdateAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
            _lastCheck = result;

            IsUpdateAvailable = result.IsUpdateAvailable;
            AvailableVersion = result.AvailableVersion;
            IsMandatory = result.IsMandatory;
            CheckMessage = result.Message;

            if (result.Manifest is not null && !string.IsNullOrWhiteSpace(UpdatePath))
            {
                ReleaseNotes = await _updates
                    .ReadReleaseNotesAsync(result.Manifest, UpdatePath, cancellationToken)
                    .ConfigureAwait(true);
            }

            // Das Ergebnis der Pruefsummenpruefung steht schon hier, nicht erst
            // beim Klick auf "Installieren" - dann weiss der Anwender vorher,
            // woran er ist.
            if (result.IsUpdateAvailable && result.InstallerFullPath is not null)
            {
                var pruefung = await _updates
                    .VerifyInstallerAsync(result.InstallerFullPath, result.Manifest?.Checksum, cancellationToken)
                    .ConfigureAwait(true);

                IsChecksumValid = pruefung.IsValid;
                ChecksumMessage = pruefung.Message;
            }
            else
            {
                IsChecksumValid = false;
                ChecksumMessage = null;
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (_lastCheck?.InstallerFullPath is null)
        {
            _dialogs.ShowInformation("Es steht derzeit kein Update zur Verfügung.", "Update");
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll die Version {_lastCheck.AvailableVersion} jetzt installiert werden?" +
                Environment.NewLine + Environment.NewLine +
                "Das Vehistra wird dazu beendet. Vor einer Datenbankänderung wird " +
                "automatisch eine Sicherung erstellt.",
                "Update installieren"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var started = await _updates.LaunchUpdaterAsync(new UpdateLaunchRequest
            {
                InstallerPath = _lastCheck.InstallerFullPath,
                TargetVersion = _lastCheck.AvailableVersion ?? string.Empty,
                CreateBackupBeforeMigration = true,
                UserName = _currentUser.User?.UserName,
                // Der Dienst prueft damit, ob das Paket unverändert ist, und
                // startet sonst nichts.
                ExpectedChecksum = _lastCheck.Manifest?.Checksum
            }).ConfigureAwait(true);

            if (started)
            {
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                ErrorMessage = "Die Updatekomponente konnte nicht gestartet werden.";
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MigrateDatabaseAsync()
    {
        if (PendingMigrations == 0)
        {
            _dialogs.ShowInformation("Das Datenbankschema ist bereits aktuell.", "Datenbank");
            return;
        }

        if (!_dialogs.Confirm(
                $"Es stehen {PendingMigrations} Datenbankänderung(en) an." + Environment.NewLine + Environment.NewLine +
                "Vor der Ausführung wird automatisch eine Sicherung erstellt. Schlägt die Sicherung fehl, " +
                "wird das Update aus Sicherheitsgründen abgebrochen." + Environment.NewLine + Environment.NewLine +
                "Bitte stellen Sie sicher, dass alle anderen Arbeitsplätze das Programm beendet haben.",
                "Datenbank aktualisieren"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var progress = new Progress<string>(text => MigrationProgress = text);

            var result = await _databaseAdministration.MigrateAsync(new MigrationOptions
            {
                CreateBackup = true,
                AbortWhenBackupFails = true,
                BackupDirectory = _connectionStore.Load()?.BackupPath,
                ApplicationVersion = InstalledVersion,
                UserName = _currentUser.User?.UserName
            }, progress).ConfigureAwait(true);

            MigrationProgress = null;

            if (result.IsSuccessful)
            {
                _dialogs.ShowInformation(
                    result.AppliedMigrations.Count == 0
                        ? "Das Datenbankschema war bereits aktuell."
                        : $"Die Datenbank wurde erfolgreich aktualisiert ({result.AppliedMigrations.Count} Änderungen)." +
                          Environment.NewLine + Environment.NewLine +
                          $"Sicherung: {result.BackupPath}",
                    "Datenbankupdate");
            }
            else
            {
                _dialogs.ShowError(
                    result.ErrorMessage + Environment.NewLine + Environment.NewLine + result.RestoreInstructions,
                    null, "Datenbankupdate fehlgeschlagen");
            }

            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenUpdateFolder()
    {
        if (!string.IsNullOrWhiteSpace(UpdatePath))
        {
            _dialogs.OpenInShell(UpdatePath);
        }
        else
        {
            _dialogs.ShowWarning(
                "Es ist keine Updateablage konfiguriert. Bitte tragen Sie den Pfad in den Servereinstellungen ein.",
                "Updateablage");
        }
    }
}
