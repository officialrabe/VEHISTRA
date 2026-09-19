using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Application.Abstractions;

/// <summary>Dokumentenverwaltung (Metadaten in SQL, Dateien auf der Serverfreigabe).</summary>
public interface IDocumentService
{
    Task<IReadOnlyList<DocumentListItem>> GetListAsync(
        int? vehicleId = null,
        DocumentCategory? category = null,
        string? searchText = null,
        CancellationToken cancellationToken = default);

    Task<VehicleDocument?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Uebernimmt eine Datei in die zentrale Ablage und speichert die Metadaten.</summary>
    Task<int> AddAsync(
        Stream content,
        string originalFileName,
        VehicleDocument metadata,
        CancellationToken cancellationToken = default);

    Task<int> AddFromFileAsync(string sourcePath, VehicleDocument metadata, CancellationToken cancellationToken = default);

    Task UpdateMetadataAsync(VehicleDocument document, CancellationToken cancellationToken = default);

    Task ArchiveAsync(int documentId, CancellationToken cancellationToken = default);

    /// <summary>Vollstaendiger Pfad der Datei in der Ablage.</summary>
    Task<string> GetFullPathAsync(int documentId, CancellationToken cancellationToken = default);

    Task LinkToDamageAsync(int documentId, int damageId, string? caption, CancellationToken cancellationToken = default);

    Task LinkToAccidentAsync(int documentId, int accidentId, string? caption, CancellationToken cancellationToken = default);

    Task LinkToWorkshopOrderAsync(int documentId, int orderId, DocumentCategory category, CancellationToken cancellationToken = default);
}
