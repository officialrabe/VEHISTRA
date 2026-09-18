using System.Windows;

namespace Fuhrpark.Client.Views.Dialogs;

/// <summary>Fehlerdialog mit ausklappbaren Details und kopierbarem Fehlerbericht.</summary>
public partial class ErrorDialog : Window
{
    private string _report = string.Empty;

    public ErrorDialog()
    {
        InitializeComponent();
    }

    public void Initialize(string title, string message, string report)
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        DetailsText.Text = report;
        _report = report;
    }

    private void OnToggleDetails(object sender, RoutedEventArgs e)
    {
        DetailsPanel.Visibility = DetailsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnCopyReport(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_report);
            MessageBox.Show(this, "Der Fehlerbericht wurde in die Zwischenablage kopiert.",
                "Fehlerbericht", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception)
        {
            MessageBox.Show(this, "Der Fehlerbericht konnte nicht in die Zwischenablage kopiert werden.",
                "Fehlerbericht", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
