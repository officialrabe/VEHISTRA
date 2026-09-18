using System.IO;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Fuhrpark.Client.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Fuhrpark.Client.Services;

/// <inheritdoc />
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DialogService> _logger;

    public DialogService(IServiceProvider services, ILogger<DialogService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void ShowInformation(string message, string title = "Hinweis") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "Warnung") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, Exception? exception = null, string title = "Fehler")
    {
        _logger.LogError(exception, "Fehlerdialog: {Message}", message);

        var window = new ErrorDialog
        {
            Owner = Owner
        };

        window.Initialize(title, message, BuildErrorReport(message, exception));
        window.ShowDialog();
    }

    public bool Confirm(string message, string title = "Bestätigen") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? Prompt(string message, string title = "Eingabe", string? initialValue = null)
    {
        var window = new PromptDialog
        {
            Owner = Owner
        };

        window.Initialize(title, message, initialValue);
        return window.ShowDialog() == true ? window.Value : null;
    }

    public string? OpenFile(string filter, string title = "Datei auswählen")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string> OpenFiles(string filter, string title = "Dateien auswählen")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true,
            Multiselect = true
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileNames : [];
    }

    public string? SaveFile(string filter, string defaultFileName, string title = "Speichern unter")
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title,
            FileName = defaultFileName,
            OverwritePrompt = true,
            AddExtension = true
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? SelectFolder(string description)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description,
            Multiselect = false
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public ConcurrencyResolution ResolveConcurrencyConflict(string entityDescription, string? differences)
    {
        var window = new ConcurrencyConflictDialog
        {
            Owner = Owner
        };

        window.Initialize(entityDescription, differences);
        window.ShowDialog();

        return window.Resolution;
    }

    public bool? ShowDialog(object viewModel)
    {
        var window = _services.GetRequiredService<DialogHostWindow>();
        window.Owner = Owner;
        window.Initialize(viewModel);

        return window.ShowDialog();
    }

    public void OpenInShell(string path)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Der Pfad konnte nicht geoeffnet werden: {Path}", path);
            ShowWarning($"Der Pfad konnte nicht geöffnet werden:{Environment.NewLine}{path}");
        }
    }

    /// <summary>Aktives Fenster als Besitzer des Dialogs.</summary>
    private static Window? Owner =>
        System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
        ?? System.Windows.Application.Current?.MainWindow;

    /// <summary>
    /// Baut den kopierbaren Fehlerbericht auf. Er enthaelt keine Zugangsdaten
    /// und keine personenbezogenen Fahrzeugdaten.
    /// </summary>
    private static string BuildErrorReport(string message, Exception? exception)
    {
        var builder = new StringBuilder();

        builder.AppendLine("FUHRPARKMANAGEMENT - FEHLERBERICHT");
        builder.AppendLine($"Zeitpunkt      : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        builder.AppendLine($"Programmversion: {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unbekannt"}");
        builder.AppendLine($"Computer       : {Environment.MachineName}");
        builder.AppendLine($"Betriebssystem : {Environment.OSVersion}");
        builder.AppendLine();
        builder.AppendLine("MELDUNG");
        builder.AppendLine(message);

        if (exception is not null)
        {
            builder.AppendLine();
            builder.AppendLine("TECHNISCHE DETAILS");
            builder.AppendLine(exception.GetType().FullName);
            builder.AppendLine(exception.Message);
            builder.AppendLine(exception.StackTrace);

            var inner = exception.InnerException;
            var depth = 0;

            while (inner is not null && depth++ < 3)
            {
                builder.AppendLine();
                builder.AppendLine($"URSACHE {depth}: {inner.GetType().Name}");
                builder.AppendLine(inner.Message);
                inner = inner.InnerException;
            }
        }

        builder.AppendLine();
        builder.AppendLine("Support: hallo@LeonLSP.dev · LSP Virtual Services");

        return builder.ToString();
    }
}
