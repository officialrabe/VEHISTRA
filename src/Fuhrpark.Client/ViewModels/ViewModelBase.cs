using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Fuhrpark.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fuhrpark.Client.ViewModels;

/// <summary>
/// Basisklasse aller Ansichtsmodelle. Kapselt Ladezustand und einheitliche Fehlerbehandlung,
/// damit dem Anwender niemals eine ungefilterte .NET-Ausnahme angezeigt wird.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Titel der Ansicht (Kopfzeile des Hauptfensters).</summary>
    public virtual string Title => string.Empty;

    /// <summary>Kurzbeschreibung unterhalb des Titels.</summary>
    public virtual string? Subtitle => null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Wird beim Wechsel auf diese Ansicht aufgerufen.</summary>
    public virtual Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Fuehrt eine Aktion aus, zeigt dabei den Ladezustand an und wandelt Ausnahmen
    /// in verstaendliche Meldungen um.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> action, string? successMessage = null)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await action().ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                StatusMessage = successMessage;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            ErrorMessage = Describe(exception);
            return false;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasError));
        }
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>Uebersetzt technische Ausnahmen in verstaendliche deutsche Meldungen.</summary>
    public static string Describe(Exception exception) => exception switch
    {
        DomainException domain => domain.Message,

        DbUpdateConcurrencyException =>
            "Dieser Datensatz wurde zwischenzeitlich von einem anderen Benutzer geändert. " +
            "Bitte laden Sie die aktuellen Daten und wiederholen Sie Ihre Änderung.",

        DbUpdateException { InnerException.Message: { } inner } when
            inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
            inner.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) =>
            "Ein Datensatz mit diesen Angaben existiert bereits.",

        DbUpdateException =>
            "Die Änderung konnte nicht gespeichert werden. Bitte prüfen Sie Ihre Eingaben.",

        Microsoft.Data.SqlClient.SqlException =>
            "Die Verbindung zur Fuhrparkdatenbank ist gestört. " +
            "Bitte prüfen Sie über 'Hilfe & Support' die Systemdiagnose.",

        UnauthorizedAccessException =>
            "Für diesen Zugriff fehlen die Berechtigungen im Dateisystem.",

        IOException io => $"Auf eine Datei konnte nicht zugegriffen werden: {io.Message}",

        _ => $"Es ist ein unerwarteter Fehler aufgetreten: {exception.Message}"
    };
}
