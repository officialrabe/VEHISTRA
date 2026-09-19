using System.Windows;
using Vehistra.Client.Services;

namespace Vehistra.Client.Views.Dialogs;

/// <summary>
/// Dialog bei gleichzeitiger Bearbeitung durch mehrere Arbeitsplaetze.
/// Die Aenderung des anderen Benutzers wird niemals stillschweigend ueberschrieben.
/// </summary>
public partial class ConcurrencyConflictDialog : Window
{
    public ConcurrencyConflictDialog()
    {
        InitializeComponent();
    }

    public ConcurrencyResolution Resolution { get; private set; } = ConcurrencyResolution.Cancel;

    public void Initialize(string entityDescription, string? differences)
    {
        MessageText.Text =
            $"Der Datensatz „{entityDescription}“ wurde zwischenzeitlich von einem anderen Benutzer geändert." +
            Environment.NewLine + Environment.NewLine +
            "Ihre Änderungen wurden nicht gespeichert, damit die Arbeit des anderen Benutzers erhalten bleibt.";

        DifferenceText.Text = differences ?? "Es liegen keine Angaben zu den Unterschieden vor.";
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        Resolution = ConcurrencyResolution.Reload;
        DialogResult = true;
    }

    private void OnCompare(object sender, RoutedEventArgs e)
    {
        Resolution = ConcurrencyResolution.Compare;
        DifferencePanel.Visibility = Visibility.Visible;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Resolution = ConcurrencyResolution.Cancel;
        DialogResult = false;
    }
}
