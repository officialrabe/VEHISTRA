using System.Windows;
using Vehistra.Application.Abstractions;
using Vehistra.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.Views;

/// <summary>
/// Wird angezeigt, wenn die Datenbank nicht erreichbar ist. Die Anwendung stuerzt
/// niemals ab und legt auch keine lokale Ersatzdatenbank an.
/// </summary>
public partial class ServerUnavailableWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IDialogService _dialogs;

    private ServerConnectionSettings? _settings;

    public ServerUnavailableWindow(IServiceProvider services, IDialogService dialogs)
    {
        InitializeComponent();

        _services = services;
        _dialogs = dialogs;
    }

    public void Initialize(ServerConnectionSettings settings)
    {
        _settings = settings;

        if (settings.IsSingleWorkstation)
        {
            // Beim Solo-Platz gibt es keinen Server. Ein Fenster, das nach dem
            // SQL-Server-Dienst und der Firewall fragt, wuerde nur in die Irre
            // fuehren.
            HeadlineText.Text = "DATENBANKDATEI NICHT ERREICHBAR";
            ServerLabel.Text = "Betriebsart";
            ServerText.Text = "Solo-Platz – die Datenbank liegt auf diesem Computer";
            DatabaseLabel.Text = "Datenbankdatei";
            DatabaseText.Text = settings.DatabaseFile ?? "nicht hinterlegt";

            DiagnosticsText.Text =
                "Bitte prüfen Sie:" + Environment.NewLine + Environment.NewLine +
                "1. Ist die Datenbankdatei noch vorhanden? Wurde der Ordner verschoben?" + Environment.NewLine +
                "2. Läuft Vehistra vielleicht schon ein zweites Mal?" + Environment.NewLine +
                "3. Darf Ihr Windows-Konto in den Ordner schreiben?" + Environment.NewLine +
                "4. Liegt die Datei auf einem Wechseldatenträger, der nicht angeschlossen ist?" +
                Environment.NewLine +
                "5. Wurde die Einrichtung schon durchgeführt (VehistraServerSetup.exe)?" +
                Environment.NewLine + Environment.NewLine +
                "Mit „Diagnose“ führen Sie eine automatische Prüfung durch. Eine Sicherung " +
                "spielen Sie nach BACKUP-UND-WIEDERHERSTELLUNG.pdf, Kapitel 9.3, zurück.";

            return;
        }

        ServerText.Text = settings.Server;
        DatabaseText.Text = settings.Database;
    }

    private void OnRetry(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private async void OnDiagnose(object sender, RoutedEventArgs e)
    {
        DiagnosticsText.Text = "Diagnose läuft ...";

        try
        {
            using var scope = _services.CreateScope();
            var diagnostics = scope.ServiceProvider.GetRequiredService<IDiagnosticsService>();

            var report = await diagnostics.RunAsync().ConfigureAwait(true);
            DiagnosticsText.Text = diagnostics.FormatForClipboard(report);
        }
        catch (Exception exception)
        {
            DiagnosticsText.Text =
                "Die Diagnose konnte nicht vollständig ausgeführt werden." + Environment.NewLine +
                exception.Message;
        }
    }

    private void OnServerSettings(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<ServerSettingsWindow>();
        window.Owner = this;

        if (window.ShowDialog() == true)
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
