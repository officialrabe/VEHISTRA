using System.Windows;
using System.Windows.Threading;

namespace Vehistra.Client.Views;

/// <summary>Kurzer Startbildschirm waehrend des Verbindungsaufbaus.</summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();

        VersionText.Text = $"Version {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }
}
