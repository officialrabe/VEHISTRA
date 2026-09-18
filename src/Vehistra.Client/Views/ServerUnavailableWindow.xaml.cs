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
