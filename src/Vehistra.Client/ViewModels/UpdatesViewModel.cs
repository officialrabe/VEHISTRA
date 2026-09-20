using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Client.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Client.ViewModels;

/// <summary>
/// Updateverwaltung: Pruefung der zentralen Updateablage, Anzeige der Release Notes,
/// Start des Updaters und Ausfuehrung ausstehender Datenbankmigrationen.
/// </summary>
public sealed partial class UpdatesViewModel : ViewModelBase
{
    private readonly IUpdateService _updates;
    private readonly IOnlineUpdateSource _online;
    private readonly ISettingsService _settings;
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

    /// <summary>
    /// Steht nur da, solange den Stand des Schemas niemand vermerkt hat - eine
    /// frisch eingerichtete Datenbank hat keine Migration hinter sich, an die
    /// sich ein Vermerk haengen koennte.
    /// </summary>
    [ObservableProperty]
    private string? _databaseSchemaHint;

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
    private OnlineUpdateInfo? _lastOnlineCheck;

    /// <summary>Quelle der Updates: Ablage im Firmennetz oder Veroeffentlichungen im Internet.</summary>
    [ObservableProperty]
    private bool _useOnlineSource;

    [ObservableProperty]
    private string _updateRepository = GitHubUpdateSource.DefaultRepository;

    [ObservableProperty]
    private string? _downloadProgress;

    [ObservableProperty]
    private bool _isDownloadReady;

    /// <summary>Noch nichts geladen, also auch nichts zu pruefen - weder gut noch schlecht.</summary>
    [ObservableProperty]
    private bool _isChecksumPending;

    public UpdatesViewModel(
        IUpdateService updates,
        IOnlineUpdateSource online,
        ISettingsService settings,
        IDatabaseAdministrationService databaseAdministration,
        IConnectionSettingsStore connectionStore,
        ICurrentUserService currentUser,
        VehistraDbContext db,
        IDialogService dialogs)
    {
        _updates = updates;
        _online = online;
        _settings = settings;
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

            var quelle = await _settings
                .GetOrDefaultAsync(SettingsKeys.UpdateSource, "Ablage", cancellationToken).ConfigureAwait(true);
            _quelleWirdGeladen = true;
            UseOnlineSource = string.Equals(quelle, "GitHub", StringComparison.OrdinalIgnoreCase);
            _quelleWirdGeladen = false;

            UpdateRepository = await _settings
                .GetOrDefaultAsync(SettingsKeys.UpdateRepository, GitHubUpdateSource.DefaultRepository,
                    cancellationToken).ConfigureAwait(true);

            var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(true);
            PendingMigrations = pending.Count();

            var current = await _db.DatabaseVersions
                .AsNoTracking()
                .Where(v => v.IsCurrent)
                .OrderByDescending(v => v.AppliedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(true);

            DatabaseSchemaVersion = current?.SchemaVersion ?? "–";
            DatabaseSchemaHint = current is null
                ? "Stand noch nicht vermerkt. „Datenbank aktualisieren“ trägt ihn nach."
                : null;

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
        if (UseOnlineSource)
        {
            await CheckOnlineAsync(cancellationToken).ConfigureAwait(true);
            return;
        }

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

                IsChecksumPending = false;
                IsChecksumValid = pruefung.IsValid;
                ChecksumMessage = pruefung.Message;
            }
            else
            {
                IsChecksumPending = false;
                IsChecksumValid = false;
                ChecksumMessage = null;
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Fragt die Veroeffentlichungen des Projekts ab. Der einzige Weg des
    /// Programms ins Internet - deshalb nur auf Klick oder wenn diese Quelle
    /// ausdruecklich eingestellt ist.
    /// </summary>
    private async Task CheckOnlineAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            var ergebnis = await _online.CheckAsync(InstalledVersion, cancellationToken).ConfigureAwait(true);

            _lastOnlineCheck = ergebnis;
            _lastCheck = null;

            IsUpdateAvailable = ergebnis.IsUpdateAvailable;
            AvailableVersion = ergebnis.AvailableVersion;
            IsMandatory = false;
            CheckMessage = ergebnis.Message;
            ReleaseNotes = ergebnis.ReleaseNotes;

            // Heruntergeladen ist noch nichts, also gibt es auch nichts zu
            // pruefen. Die Pruefsumme kommt nach dem Download.
            IsDownloadReady = false;
            IsChecksumValid = false;
            IsChecksumPending = ergebnis.IsUpdateAvailable;
            ChecksumMessage = ergebnis.IsUpdateAvailable
                ? "Das Paket wird beim Installieren geladen und gegen checksums.sha256 geprüft."
                : null;

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Laedt das Paket und prueft die Pruefsumme. Rueckgabe <c>true</c>, wenn
    /// danach ein geprueftes Paket bereitliegt.
    /// </summary>
    private async Task<bool> HoleUpdateAsync(bool fragen)
    {
        if (_lastOnlineCheck is not { IsUpdateAvailable: true })
        {
            _dialogs.ShowInformation("Es steht derzeit kein Update zum Herunterladen bereit.", "Update");
            return false;
        }

        var info = _lastOnlineCheck;

        if (fragen && !_dialogs.Confirm(
                $"Version {info.AvailableVersion} jetzt von der Veröffentlichungsseite herunterladen?" +
                Environment.NewLine + Environment.NewLine +
                "Dabei werden zwei Dateien geladen: das Updatepaket und die Prüfsummendatei. " +
                "Es werden keine Daten aus dem Fuhrpark übertragen." + Environment.NewLine + Environment.NewLine +
                "Installiert wird erst danach, auf einen weiteren Klick.",
                "Update herunterladen"))
        {
            return false;
        }

        return await RunAsync(async () =>
        {
            var fortschritt = new Progress<OnlineUpdateProgress>(stand =>
                DownloadProgress = stand.Percent is { } prozent
                    ? $"{stand.Step}: {prozent} %"
                    : stand.Step);

            var paket = await _online.DownloadAsync(info, fortschritt).ConfigureAwait(true);

            // Geprueft wird mit derselben Stelle wie bei der Updateablage:
            // erwartet wird der Wert aus checksums.sha256 neben dem Paket.
            var pruefung = await _updates.VerifyInstallerAsync(paket, null).ConfigureAwait(true);

            IsChecksumPending = false;
            IsChecksumValid = pruefung.IsValid;
            ChecksumMessage = pruefung.Message;
            IsDownloadReady = pruefung.IsValid;
            DownloadProgress = pruefung.IsValid
                ? $"Heruntergeladen und geprüft: {paket}"
                : "Die Prüfung ist fehlgeschlagen - das Paket wird nicht ausgeführt.";

            _lastCheck = pruefung.IsValid
                ? new UpdateCheckResult(
                    true, InstalledVersion, info.AvailableVersion, false, null, paket, info.Message, true)
                : null;
        }).ConfigureAwait(true) && IsDownloadReady;
    }

    /// <summary>Merkt die gewaehlte Quelle - mit einem Wort dazu, was das bedeutet.</summary>
    partial void OnUseOnlineSourceChanged(bool value)
    {
        if (_quelleWirdGeladen)
        {
            return;
        }

        if (value && !_dialogs.Confirm(
                "Updates künftig unmittelbar von der Veröffentlichungsseite des Projekts beziehen?" +
                Environment.NewLine + Environment.NewLine +
                "Das Programm fragt dann bei einer Updateprüfung über das Internet nach der neuesten " +
                "Version. Übertragen wird nur die Anfrage selbst - keine Fahrzeug-, Fahrer- oder " +
                "Betriebsdaten und keine Kennung dieses Arbeitsplatzes." + Environment.NewLine +
                Environment.NewLine +
                "Ist zusätzlich „Beim Start nach Updates suchen“ eingeschaltet, geschieht diese Anfrage " +
                "bei jedem Programmstart." + Environment.NewLine + Environment.NewLine +
                "Heruntergeladene Pakete werden wie bisher gegen ihre Prüfsumme geprüft. " +
                "Ohne Internetverbindung bleibt die Updateablage im Firmennetz der Weg.",
                "Updates aus dem Internet"))
        {
            _quelleWirdGeladen = true;
            UseOnlineSource = false;
            _quelleWirdGeladen = false;
            return;
        }

        _ = SpeichereQuelleAsync(value);
    }

    private async Task SpeichereQuelleAsync(bool online)
    {
        await RunAsync(async () =>
        {
            await _settings.SetAsync(SettingsKeys.UpdateSource, online ? "GitHub" : "Ablage").ConfigureAwait(true);

            if (online && !string.IsNullOrWhiteSpace(UpdateRepository))
            {
                await _settings.SetAsync(SettingsKeys.UpdateRepository, UpdateRepository.Trim()).ConfigureAwait(true);
            }

            await CheckAsync().ConfigureAwait(true);
        }, online
            ? "Updates kommen jetzt von der Veröffentlichungsseite."
            : "Updates kommen jetzt aus der Updateablage im Firmennetz.").ConfigureAwait(true);
    }

    /// <summary>Verhindert die Rueckfrage, waehrend die Einstellung gelesen wird.</summary>
    private bool _quelleWirdGeladen;

    /// <summary>
    /// Installiert das Update - und holt es bei der Onlinequelle vorher selbst.
    ///
    /// Zwei Schaltflaechen waren eine schlechte Idee: wer "Update installieren"
    /// drueckt, will das Update, nicht erst einen Zwischenschritt. Ohne
    /// heruntergeladenes Paket kam vorher nur "Es steht derzeit kein Update zur
    /// Verfuegung" - obwohl daneben stand, dass eines bereitsteht.
    /// </summary>
    [RelayCommand]
    private async Task InstallAsync()
    {
        // Onlinequelle und noch nichts geholt: herunterladen gehoert zum Klick.
        if (UseOnlineSource && _lastCheck?.InstallerFullPath is null)
        {
            if (_lastOnlineCheck is not { IsUpdateAvailable: true })
            {
                _dialogs.ShowInformation("Es steht derzeit kein Update zur Verfügung.", "Update");
                return;
            }

            if (!_dialogs.Confirm(
                    $"Version {_lastOnlineCheck.AvailableVersion} jetzt herunterladen und installieren?" +
                    Environment.NewLine + Environment.NewLine +
                    "Geladen werden zwei Dateien von der Veröffentlichungsseite: das Updatepaket und " +
                    "die Prüfsummendatei. Es werden keine Daten aus dem Fuhrpark übertragen." +
                    Environment.NewLine + Environment.NewLine +
                    "Stimmt die Prüfsumme nicht, wird nichts ausgeführt. Sonst wird Vehistra beendet " +
                    "und das Update gestartet; vor einer Datenbankänderung wird automatisch gesichert.",
                    "Update installieren"))
            {
                return;
            }

            if (!await HoleUpdateAsync(fragen: false).ConfigureAwait(true))
            {
                _dialogs.ShowError(
                    ErrorMessage ?? ChecksumMessage ?? "Das Update konnte nicht geladen werden.",
                    null,
                    "Update installieren");
                return;
            }
        }
        else
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
        }

        if (_lastCheck?.InstallerFullPath is null)
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
