using System.IO;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>
/// Hilfe- und Supportbereich mit Systemdiagnose, Supportpaket und Angaben zum Hersteller.
/// </summary>
public sealed partial class SupportViewModel : ViewModelBase
{
    public const string SupportMail = "support@vehistra.dev";
    public const string SupportWebsite = "vehistra.dev";
    public const string Developer = "LSP Virtual Services";
    public const string ProductName = "Vehistra";
    public const string Tagline = "Open Fleet Management";
    public const string Scope = "Vehicle · Driver · Maintenance · Compliance";

    private readonly IDiagnosticsService _diagnostics;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private DiagnosticsReport? _report;

    [ObservableProperty]
    private string? _diagnosticsText;

    public SupportViewModel(IDiagnosticsService diagnostics, IDialogService dialogs, IServiceProvider services)
    {
        _diagnostics = diagnostics;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Hilfe & Support";

    public override string? Subtitle => $"Vehistra {Version} · entwickelt und supportet von {Developer}";

    public string Version => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public string LogDirectory => ApplicationPaths.Logs;

    public string ProductTitle => ProductName;

    public string ProductTagline => Tagline;

    public string ProductScope => Scope;

    public string DeveloperLine => $"Developed & maintained by {Developer}";

    public string DeveloperName => Developer;

    public string Website => SupportWebsite;

    public string SupportAddress => SupportMail;

    public string Copyright => $"© {Developer}";

    public override async Task LoadAsync(CancellationToken cancellationToken = default) =>
        await RunDiagnosticsAsync(cancellationToken).ConfigureAwait(true);

    [RelayCommand]
    private async Task RunDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            Report = await _diagnostics.RunAsync(cancellationToken).ConfigureAwait(true);
            DiagnosticsText = _diagnostics.FormatForClipboard(Report);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        if (string.IsNullOrWhiteSpace(DiagnosticsText))
        {
            return;
        }

        try
        {
            Clipboard.SetText(DiagnosticsText);
            StatusMessage = "Die Diagnose wurde in die Zwischenablage kopiert.";
        }
        catch (Exception exception)
        {
            ErrorMessage = Describe(exception);
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        Directory.CreateDirectory(ApplicationPaths.Logs);
        _dialogs.OpenInShell(ApplicationPaths.Logs);
    }

    [RelayCommand]
    private async Task CreateSupportPackageAsync()
    {
        var folder = _dialogs.SelectFolder("Zielverzeichnis für das Supportpaket auswählen");

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var path = await _diagnostics.CreateSupportPackageAsync(folder).ConfigureAwait(true);

            _dialogs.ShowInformation(
                "Das Supportpaket wurde erstellt:" + Environment.NewLine + path + Environment.NewLine +
                Environment.NewLine +
                "Es enthält Logdateien, Versions- und Systemangaben – keine Passwörter und keine " +
                "Fahrzeug- oder Personendaten.",
                "Supportpaket");
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ContactSupport()
    {
        // Es werden ausschliesslich technische Angaben vorbereitet - keine Fahrzeug- oder Personendaten.
        var body = string.Join(Environment.NewLine,
            "Bitte beschreiben Sie Ihr Anliegen:",
            string.Empty,
            string.Empty,
            "---------------------------------------------",
            "Technische Angaben (automatisch ergänzt):",
            $"Programmversion : {Version}",
            $"Datenbankschema : {Report?.DatabaseSchemaVersion ?? "unbekannt"}",
            $"Computername    : {Environment.MachineName}",
            $"Windows         : {Environment.OSVersion}",
            "---------------------------------------------");

        var uri = $"mailto:{SupportMail}" +
                  $"?subject={Uri.EscapeDataString("Supportanfrage – Vehistra")}" +
                  $"&body={Uri.EscapeDataString(body)}";

        try
        {
            using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            _dialogs.ShowInformation(
                $"Das Standard-Mailprogramm konnte nicht geöffnet werden.{Environment.NewLine}{Environment.NewLine}" +
                $"Bitte senden Sie Ihre Anfrage an: {SupportMail}",
                "Support kontaktieren");
        }
    }

    [RelayCommand]
    private void ChangePassword()
    {
        var dialog = _services.GetRequiredService<ChangePasswordViewModel>();
        _dialogs.ShowDialog(dialog);
    }

    [RelayCommand]
    private void OpenDocumentation()
    {
        var candidates = new[]
        {
            Path.Combine(ApplicationPaths.InstallDirectory, "Documentation"),
            Path.Combine(ApplicationPaths.InstallDirectory, "Dokumentation"),
            Path.Combine(ApplicationPaths.MachineData, "Documentation")
        };

        var folder = candidates.FirstOrDefault(Directory.Exists);

        if (folder is null)
        {
            _dialogs.ShowInformation(
                "Die Dokumentation wurde nicht gefunden. Sie liegt im Installationsverzeichnis " +
                "im Ordner „Documentation“ und enthält unter anderem SERVER-EINRICHTUNG-EINFACH.pdf.",
                "Dokumentation");
            return;
        }

        _dialogs.OpenInShell(folder);
    }
}
