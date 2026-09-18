namespace Fuhrpark.Client.Services;

/// <summary>Dialoge und Meldungen. Wird von den Ansichtsmodellen verwendet, ohne WPF-Typen zu kennen.</summary>
public interface IDialogService
{
    void ShowInformation(string message, string title = "Hinweis");

    void ShowWarning(string message, string title = "Warnung");

    /// <summary>Zeigt einen Fehler mit den Schaltflaechen 'Details' und 'Fehlerbericht kopieren'.</summary>
    void ShowError(string message, Exception? exception = null, string title = "Fehler");

    bool Confirm(string message, string title = "Bestätigen");

    /// <summary>Freitexteingabe, z. B. fuer einen Grund.</summary>
    string? Prompt(string message, string title = "Eingabe", string? initialValue = null);

    string? OpenFile(string filter, string title = "Datei auswählen");

    IReadOnlyList<string> OpenFiles(string filter, string title = "Dateien auswählen");

    string? SaveFile(string filter, string defaultFileName, string title = "Speichern unter");

    string? SelectFolder(string description);

    /// <summary>
    /// Zeigt den Konfliktdialog bei gleichzeitiger Bearbeitung durch mehrere Benutzer.
    /// </summary>
    ConcurrencyResolution ResolveConcurrencyConflict(string entityDescription, string? differences);

    /// <summary>Oeffnet ein modales Fenster zu einem Ansichtsmodell und gibt zurueck, ob gespeichert wurde.</summary>
    bool? ShowDialog(object viewModel);

    void OpenInShell(string path);
}

/// <summary>Auswahl des Benutzers im Nebenlaeufigkeitskonflikt.</summary>
public enum ConcurrencyResolution
{
    /// <summary>Aktuelle Daten neu laden und eigene Aenderungen verwerfen.</summary>
    Reload,

    /// <summary>Unterschiede anzeigen.</summary>
    Compare,

    /// <summary>Vorgang abbrechen.</summary>
    Cancel
}
