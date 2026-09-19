using System.Security.Cryptography;
using Vehistra.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.Storage;

/// <summary>
/// Dokumentenablage auf einer Windows-Freigabe. Dateien werden strukturiert abgelegt;
/// in der Datenbank stehen ausschliesslich Metadaten.
/// </summary>
public sealed class FileSystemDocumentStorage : IDocumentStorage
{
    /// <summary>Zulaessige Dateiendungen. Ausfuehrbare Dateien sind bewusst ausgeschlossen.</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".heic",
        ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".rtf", ".odt", ".ods", ".msg", ".eml", ".zip"
    };

    /// <summary>Maximale Dateigroesse: 50 MB. Harte Obergrenze der Ablage.</summary>
    public const long MaxFileSizeBytes = 50L * 1024 * 1024;

    /// <summary>
    /// Signaturen am Dateianfang, an denen sich ausfuehrbarer Inhalt erkennen
    /// laesst. Was hier passt, wird abgelehnt - egal wie die Datei heisst. Eine
    /// zu "Rechnung.pdf" umbenannte EXE kommt damit nicht in die Ablage.
    /// </summary>
    private static readonly (byte[] Muster, string Bezeichnung)[] AusfuehrbareSignaturen =
    [
        ([0x4D, 0x5A], "Windows-Programm (EXE, DLL, MSI)"),
        ([0x7F, 0x45, 0x4C, 0x46], "Linux-Programm (ELF)"),
        ([0xCA, 0xFE, 0xBA, 0xBE], "Java-Programm"),
        ([0x23, 0x21], "Skript mit Shebang (#!)")
    ];

    /// <summary>
    /// Erwartete Signatur je Dateiendung. Fehlt eine Endung hier, hat das
    /// Format keine verlaessliche Signatur (z. B. Textdateien) - dann bleibt es
    /// bei der Pruefung auf ausfuehrbaren Inhalt.
    /// </summary>
    private static readonly Dictionary<string, (byte[] Muster, string Bezeichnung)[]> ErwarteteSignaturen =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = [([0x25, 0x50, 0x44, 0x46], "PDF")],
            [".jpg"] = [([0xFF, 0xD8, 0xFF], "JPEG")],
            [".jpeg"] = [([0xFF, 0xD8, 0xFF], "JPEG")],
            [".png"] = [([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "PNG")],
            [".gif"] = [([0x47, 0x49, 0x46, 0x38], "GIF")],
            [".bmp"] = [([0x42, 0x4D], "BMP")],
            [".tif"] = [([0x49, 0x49, 0x2A, 0x00], "TIFF"), ([0x4D, 0x4D, 0x00, 0x2A], "TIFF")],
            [".tiff"] = [([0x49, 0x49, 0x2A, 0x00], "TIFF"), ([0x4D, 0x4D, 0x00, 0x2A], "TIFF")],
            [".zip"] = [([0x50, 0x4B, 0x03, 0x04], "ZIP")],
            // Die neueren Office-Formate sind ZIP-Behaelter.
            [".docx"] = [([0x50, 0x4B, 0x03, 0x04], "Office-Dokument")],
            [".xlsx"] = [([0x50, 0x4B, 0x03, 0x04], "Office-Dokument")],
            [".ods"] = [([0x50, 0x4B, 0x03, 0x04], "Office-Dokument")],
            [".odt"] = [([0x50, 0x4B, 0x03, 0x04], "Office-Dokument")],
            // Die aelteren sind OLE-Behaelter, ebenso Outlook-Nachrichten.
            [".doc"] = [([0xD0, 0xCF, 0x11, 0xE0], "Word-Dokument (alt)")],
            [".xls"] = [([0xD0, 0xCF, 0x11, 0xE0], "Excel-Datei (alt)")],
            [".msg"] = [([0xD0, 0xCF, 0x11, 0xE0], "Outlook-Nachricht")],
            [".rtf"] = [([0x7B, 0x5C, 0x72, 0x74, 0x66], "RTF")]
        };

    private readonly ILogger<FileSystemDocumentStorage> _logger;
    private string _rootPath = string.Empty;

    public FileSystemDocumentStorage(ILogger<FileSystemDocumentStorage> logger)
    {
        _logger = logger;
    }

    public string RootPath => _rootPath;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_rootPath);

    /// <summary>Setzt das Wurzelverzeichnis (aus der Serverkonfiguration bzw. den Einstellungen).</summary>
    public void Configure(string? rootPath)
    {
        _rootPath = rootPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;
    }

    public async Task<DocumentStorageResult> StoreAsync(
        Stream content,
        string originalFileName,
        string targetFolder,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"Dateien vom Typ '{extension}' sind nicht zugelassen. " +
                $"Erlaubt sind: {string.Join(", ", AllowedExtensions.Order())}");
        }

        if (content.CanSeek && content.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Die Datei ist groesser als {MaxFileSizeBytes / 1024 / 1024} MB und kann nicht abgelegt werden.");
        }

        var safeFolder = SanitizeRelativePath(targetFolder);
        var fullFolder = Path.Combine(_rootPath, safeFolder);
        EnsureInsideRoot(fullFolder);
        Directory.CreateDirectory(fullFolder);

        var safeName = SanitizeFileName(originalFileName);
        var unique = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}_{safeName}";
        var fullPath = Path.Combine(fullFolder, unique);
        EnsureInsideRoot(fullPath);

        long size;
        string checksum;

        await using (var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                         bufferSize: 81920, useAsync: true))
        {
            await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            size = target.Length;
        }

        if (size > MaxFileSizeBytes)
        {
            File.Delete(fullPath);
            throw new InvalidOperationException(
                $"Die Datei ist groesser als {MaxFileSizeBytes / 1024 / 1024} MB und kann nicht abgelegt werden.");
        }

        // Der Inhalt entscheidet, nicht der Name. Geprueft wird nach dem
        // Schreiben, damit es auch fuer Datenstroeme funktioniert, die sich
        // nicht zuruecksetzen lassen; bei Verdacht wird die Datei geloescht.
        var inhaltsfehler = await PruefeInhaltAsync(fullPath, extension, cancellationToken).ConfigureAwait(false);

        if (inhaltsfehler is not null)
        {
            File.Delete(fullPath);
            _logger.LogWarning("Dokument abgelehnt: {Datei} - {Grund}", originalFileName, inhaltsfehler);
            throw new InvalidOperationException(inhaltsfehler);
        }

        await using (var stream = File.OpenRead(fullPath))
        {
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            checksum = Convert.ToHexString(hash);
        }

        var relativePath = Path.Combine(safeFolder, unique);
        _logger.LogInformation("Dokument abgelegt: {Path} ({Size} Bytes)", relativePath, size);

        return new DocumentStorageResult(relativePath, size, checksum, GuessContentType(extension));
    }

    /// <summary>
    /// Prueft den Dateianfang. Gibt den Grund zurueck, wenn die Datei
    /// abzulehnen ist, sonst <c>null</c>.
    /// </summary>
    private static async Task<string?> PruefeInhaltAsync(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        var kopf = new byte[8];
        int gelesen;

        await using (var stream = File.OpenRead(path))
        {
            gelesen = await stream.ReadAsync(kopf, cancellationToken).ConfigureAwait(false);
        }

        if (gelesen == 0)
        {
            return "Die Datei ist leer.";
        }

        var anfang = kopf.AsSpan(0, gelesen);

        foreach (var (muster, bezeichnung) in AusfuehrbareSignaturen)
        {
            if (Passt(anfang, muster))
            {
                return $"Die Datei enthaelt ausfuehrbaren Inhalt ({bezeichnung}) und wird nicht abgelegt, " +
                       "auch wenn die Dateiendung etwas anderes sagt.";
            }
        }

        if (!ErwarteteSignaturen.TryGetValue(extension, out var erwartet))
        {
            // Formate ohne verlaessliche Signatur, z. B. .txt, .csv, .eml.
            return null;
        }

        foreach (var (muster, _) in erwartet)
        {
            if (Passt(anfang, muster))
            {
                return null;
            }
        }

        return $"Der Inhalt der Datei passt nicht zur Endung '{extension}' " +
               $"(erwartet: {erwartet[0].Bezeichnung}). Bitte die Datei pruefen; " +
               "moeglicherweise wurde sie umbenannt oder ist beschaedigt.";
    }

    private static bool Passt(ReadOnlySpan<byte> anfang, byte[] muster) =>
        anfang.Length >= muster.Length && anfang[..muster.Length].SequenceEqual(muster);

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var fullPath = GetFullPath(relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Die Datei wurde in der Dokumentenablage nicht gefunden: {relativePath}", fullPath);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        return Task.FromResult(stream);
    }

    public string GetFullPath(string relativePath)
    {
        EnsureConfigured();

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, SanitizeRelativePath(relativePath)));
        EnsureInsideRoot(fullPath);
        return fullPath;
    }

    public bool Exists(string relativePath)
    {
        if (!IsConfigured)
        {
            return false;
        }

        try
        {
            return File.Exists(GetFullPath(relativePath));
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogWarning("Dokument geloescht: {Path}", relativePath);
        }

        return Task.CompletedTask;
    }

    public async Task<StorageProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new StorageProbeResult(false, false, false,
                "Es ist kein Dokumentenpfad konfiguriert.");
        }

        if (!Directory.Exists(_rootPath))
        {
            return new StorageProbeResult(false, false, false,
                $"Das Verzeichnis '{_rootPath}' ist nicht erreichbar. " +
                "Bitte Netzwerkfreigabe und Berechtigungen pruefen.");
        }

        var canRead = false;
        var canWrite = false;
        string? message = null;

        try
        {
            _ = Directory.EnumerateFileSystemEntries(_rootPath).Take(1).ToList();
            canRead = true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            message = $"Das Verzeichnis kann nicht gelesen werden: {exception.Message}";
        }

        var probeFile = Path.Combine(_rootPath, $".schreibtest_{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(probeFile, "Schreibtest Vehistra", cancellationToken)
                .ConfigureAwait(false);
            canWrite = true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            message ??= $"In das Verzeichnis kann nicht geschrieben werden: {exception.Message}";
        }
        finally
        {
            if (File.Exists(probeFile))
            {
                File.Delete(probeFile);
            }
        }

        message ??= "Die Dokumentenablage ist erreichbar und beschreibbar.";
        return new StorageProbeResult(true, canRead, canWrite, message);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Es ist keine Dokumentenablage konfiguriert. Bitte den Dokumentenpfad in den Einstellungen hinterlegen.");
        }
    }

    /// <summary>Schuetzt vor Verzeichniswechseln ausserhalb der Ablage (Path Traversal).</summary>
    private void EnsureInsideRoot(string fullPath)
    {
        var root = Path.GetFullPath(_rootPath);
        var target = Path.GetFullPath(fullPath);

        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "Der Zugriff ausserhalb der Dokumentenablage ist nicht zulaessig.");
        }
    }

    private static string SanitizeRelativePath(string relativePath)
    {
        var parts = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p != "." && p != "..")
            .Select(SanitizeFileName);

        return Path.Combine(parts.ToArray());
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? "datei" : cleaned;
    }

    private static string GuessContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".heic" => "image/heic",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".csv" => "text/csv",
        ".txt" => "text/plain",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}
