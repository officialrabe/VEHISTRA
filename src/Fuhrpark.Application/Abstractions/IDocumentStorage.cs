namespace Fuhrpark.Application.Abstractions;

/// <summary>
/// Zugriff auf die zentrale Dokumentenablage (Windows-Server-Freigabe).
/// Dateien werden niemals in der Datenbank abgelegt - dort stehen nur Metadaten.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>Wurzelverzeichnis der Ablage, z. B. \\FUHRPARK-SRV01\Fuhrpark\Dokumente.</summary>
    string RootPath { get; }

    bool IsConfigured { get; }

    Task<DocumentStorageResult> StoreAsync(
        Stream content,
        string originalFileName,
        string targetFolder,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    string GetFullPath(string relativePath);

    bool Exists(string relativePath);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Prueft Erreichbarkeit und Schreibrechte der Ablage.</summary>
    Task<StorageProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Ergebnis einer Dateiablage.</summary>
public sealed record DocumentStorageResult(string RelativePath, long SizeBytes, string Sha256, string ContentType);

/// <summary>Ergebnis einer Erreichbarkeitspruefung.</summary>
public sealed record StorageProbeResult(bool Exists, bool CanRead, bool CanWrite, string? Message);
