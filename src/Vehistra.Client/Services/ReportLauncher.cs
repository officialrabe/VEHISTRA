using System.IO;
using System.Diagnostics;
using Vehistra.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Vehistra.Client.Services;

/// <summary>Speichert erzeugte PDF-Berichte und oeffnet sie in der Standardanwendung.</summary>
public interface IReportLauncher
{
    /// <summary>Speichert den Bericht im Zwischenspeicher und oeffnet die Druckvorschau.</summary>
    Task<string> PreviewAsync(byte[] pdf, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Speichert den Bericht an einem vom Benutzer gewaehlten Ort.</summary>
    Task<string?> SaveAsAsync(byte[] pdf, string suggestedFileName, CancellationToken cancellationToken = default);

    /// <summary>Sendet den Bericht direkt an den Standarddrucker.</summary>
    Task PrintAsync(byte[] pdf, string fileName, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ReportLauncher : IReportLauncher
{
    private readonly IDialogService _dialogs;
    private readonly ILogger<ReportLauncher> _logger;

    public ReportLauncher(IDialogService dialogs, ILogger<ReportLauncher> logger)
    {
        _dialogs = dialogs;
        _logger = logger;
    }

    public async Task<string> PreviewAsync(
        byte[] pdf,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var path = await WriteToCacheAsync(pdf, fileName, cancellationToken).ConfigureAwait(false);
        _dialogs.OpenInShell(path);
        return path;
    }

    public async Task<string?> SaveAsAsync(
        byte[] pdf,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var target = _dialogs.SaveFile("PDF-Dokument (*.pdf)|*.pdf", suggestedFileName, "Bericht speichern unter");

        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        await File.WriteAllBytesAsync(target, pdf, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Bericht gespeichert: {Path}", target);

        return target;
    }

    public async Task PrintAsync(byte[] pdf, string fileName, CancellationToken cancellationToken = default)
    {
        var path = await WriteToCacheAsync(pdf, fileName, cancellationToken).ConfigureAwait(false);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "print",
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Direktes Drucken war nicht moeglich, es wird die Vorschau geoeffnet.");
            _dialogs.OpenInShell(path);
        }
    }

    private static async Task<string> WriteToCacheAsync(
        byte[] pdf,
        string fileName,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ApplicationPaths.ReportCache);

        var safeName = string.Concat(fileName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(ApplicationPaths.ReportCache, safeName);

        await File.WriteAllBytesAsync(path, pdf, cancellationToken).ConfigureAwait(false);
        return path;
    }
}
