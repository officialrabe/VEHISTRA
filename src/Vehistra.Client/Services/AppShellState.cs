using CommunityToolkit.Mvvm.ComponentModel;

namespace Vehistra.Client.Services;

/// <summary>Anwendungsweiter Zustand, den mehrere Ansichten gemeinsam nutzen.</summary>
public sealed partial class AppShellState : ObservableObject
{
    /// <summary>Anzahl ungelesener Benachrichtigungen (Anzeige im Hauptfenster).</summary>
    [ObservableProperty]
    private int _unreadNotifications;

    /// <summary>Unternehmensname aus den Systemeinstellungen.</summary>
    [ObservableProperty]
    private string _companyName = "Vehistra";

    /// <summary>Verfuegbare Programmversion aus der Updateablage.</summary>
    [ObservableProperty]
    private string? _availableUpdateVersion;

    [ObservableProperty]
    private bool _isUpdateMandatory;
}
