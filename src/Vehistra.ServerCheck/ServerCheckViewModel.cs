using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Vehistra.ServerCheck;

/// <summary>
/// Ansichtsmodell des Diagnoseprogramms. Zeigt den Zustand der Serverinstallation
/// und erklaert jedes Problem in einfacher Sprache.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class ServerCheckViewModel : ObservableObject
{
    private readonly ServerCheckRunner _runner;
    private readonly ILogger<ServerCheckViewModel> _logger;

    private IReadOnlyList<CheckResult> _results = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    private string _progressText = "Die Prüfung wurde noch nicht gestartet.";

    [ObservableProperty]
    private string _summaryCaption = "Bereit";

    [ObservableProperty]
    private string _summaryText =
        "Klicken Sie auf „Prüfung starten“. Der Vorgang dauert je nach Netzwerk einige Sekunden.";

    [ObservableProperty]
    private CheckState _overallState = CheckState.Skipped;

    [ObservableProperty]
    private string? _savedReportPath;

    public ServerCheckViewModel(ServerCheckRunner runner, ILogger<ServerCheckViewModel> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public ObservableCollection<CheckGroup> Groups { get; } = [];

    public string LogDirectory => ApplicationPaths.Logs;

    [RelayCommand]
    private async Task RunChecksAsync()
    {
        IsBusy = true;
        Groups.Clear();
        SavedReportPath = null;

        var progress = new Progress<string>(text => ProgressText = text);

        try
        {
            _results = await _runner.RunAsync(progress).ConfigureAwait(true);

            foreach (var group in _results.GroupBy(r => r.Group))
            {
                Groups.Add(new CheckGroup(group.Key, [.. group]));
            }

            var problems = _results.Count(r => r.State == CheckState.Problem);
            var warnings = _results.Count(r => r.State == CheckState.Warning);

            if (problems > 0)
            {
                OverallState = CheckState.Problem;
                SummaryCaption = "PROBLEM";
                SummaryText = problems == 1
                    ? "Es wurde 1 Problem gefunden. Bitte die markierten Punkte abarbeiten."
                    : $"Es wurden {problems} Probleme gefunden. Bitte die markierten Punkte abarbeiten.";
            }
            else if (warnings > 0)
            {
                OverallState = CheckState.Warning;
                SummaryCaption = "HINWEIS";
                SummaryText = $"Der Server arbeitet, es gibt aber {warnings} Hinweis(e) zur Verbesserung.";
            }
            else
            {
                OverallState = CheckState.Ok;
                SummaryCaption = "IN ORDNUNG";
                SummaryText = "Alle Prüfungen waren erfolgreich. Der Server ist einsatzbereit.";
            }

            HasRun = true;
            ProgressText = $"Prüfung abgeschlossen um {DateTime.Now:HH:mm:ss} · {_results.Count} Prüfpunkte.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Die Serverprüfung ist fehlgeschlagen.");

            OverallState = CheckState.Problem;
            SummaryCaption = "PROBLEM";
            SummaryText = "Die Prüfung konnte nicht vollständig durchgeführt werden: " + exception.Message;
            ProgressText = "Die Prüfung wurde abgebrochen.";
        }
        finally
        {
            IsBusy = false;
            CopyReportCommand.NotifyCanExecuteChanged();
            SaveReportCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanUseReport() => _results.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUseReport))]
    private void CopyReport()
    {
        System.Windows.Clipboard.SetText(_runner.FormatReport(_results));
        ProgressText = "Der Bericht wurde in die Zwischenablage kopiert.";
    }

    [RelayCommand(CanExecute = nameof(CanUseReport))]
    private void SaveReport()
    {
        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"Fuhrpark-Serverpruefung-{DateTime.Now:yyyy-MM-dd-HHmm}.txt");

        File.WriteAllText(target, _runner.FormatReport(_results));

        SavedReportPath = target;
        ProgressText = "Der Bericht wurde gespeichert: " + target;
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        Directory.CreateDirectory(ApplicationPaths.Logs);

        Process.Start(new ProcessStartInfo
        {
            FileName = ApplicationPaths.Logs,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void ContactSupport()
    {
        // Es werden ausschliesslich Betreff und Freitext vorbereitet - niemals Daten ungefragt gesendet.
        var url = "mailto:support@vehistra.dev?subject=" +
                  Uri.EscapeDataString("Supportanfrage – Vehistra");

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}

/// <summary>Gruppe zusammengehoeriger Pruefpunkte.</summary>
public sealed record CheckGroup(string Name, IReadOnlyList<CheckResult> Results)
{
    public int ProblemCount => Results.Count(r => r.State == CheckState.Problem);

    public string Summary => ProblemCount == 0
        ? $"{Results.Count} Prüfpunkt(e) · keine Probleme"
        : $"{Results.Count} Prüfpunkt(e) · {ProblemCount} Problem(e)";
}
