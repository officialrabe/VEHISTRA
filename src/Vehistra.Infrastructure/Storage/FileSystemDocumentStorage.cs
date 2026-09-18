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

    /// <summary>Maximale Dateigroesse: 50 MB.</summary>
    private const long MaxFileSizeBytes = 50L * 1024 * 1024;

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

        await using (var stream = File.OpenRead(fullPath))
        {
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            checksum = Convert.ToHexString(hash);
        }

        var relativePath = Path.Combine(safeFolder, unique);
        _logger.LogInformation("Dokument abgelegt: {Path} ({Size} Bytes)", relativePath, size);

        return new DocumentStorageResult(relativePath, size, checksum, GuessContentType(extension));
    }

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
