using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Common;
using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;
using Fuhrpark.Domain.Exceptions;
using Fuhrpark.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Fuhrpark.Application.Services;

/// <inheritdoc />
public sealed class DocumentService : IDocumentService
{
    private readonly IFuhrparkDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DocumentService(
        IFuhrparkDbContext db,
        IDocumentStorage storage,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _db = db;
        _storage = storage;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DocumentListItem>> GetListAsync(
        int? vehicleId = null,
        DocumentCategory? category = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DocumentView);

        var query = _db.VehicleDocuments.AsNoTracking().Where(d => !d.IsArchived);

        if (vehicleId is { } id)
        {
            query = query.Where(d => d.VehicleId == id);
        }

        if (category is { } documentCategory)
        {
            query = query.Where(d => d.Category == documentCategory);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var text = searchText.Trim();
            query = query.Where(d =>
                d.Title.Contains(text) ||
                d.OriginalFileName.Contains(text) ||
                (d.Description != null && d.Description.Contains(text)));
        }

        return await query
            .OrderByDescending(d => d.DocumentDate ?? d.CreatedAt)
            .Select(d => new DocumentListItem(
                d.Id,
                d.VehicleId,
                d.Vehicle != null ? (d.Vehicle.LicensePlate ?? d.Vehicle.InternalNumber) : null,
                d.Category,
                d.Title,
                d.OriginalFileName,
                d.FileSizeBytes,
                d.DocumentDate,
                d.CreatedAt,
                d.CreatedByUserName,
                d.RelativePath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VehicleDocument?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DocumentView);

        return await _db.VehicleDocuments
            .Include(d => d.Vehicle)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(
        Stream content,
        string originalFileName,
        VehicleDocument metadata,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DocumentManage);

        Guard.That(_storage.IsConfigured,
            "Es ist keine Dokumentenablage konfiguriert. Bitte in den Einstellungen den Dokumentenpfad hinterlegen.");

        var folder = await BuildTargetFolderAsync(metadata, cancellationToken).ConfigureAwait(false);

        var stored = await _storage.StoreAsync(content, originalFileName, folder, cancellationToken)
            .ConfigureAwait(false);

        metadata.OriginalFileName = originalFileName;
        metadata.RelativePath = stored.RelativePath;
        metadata.FileSizeBytes = stored.SizeBytes;
        metadata.Sha256 = stored.Sha256;
        metadata.ContentType = stored.ContentType;
        metadata.Title = string.IsNullOrWhiteSpace(metadata.Title)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : metadata.Title;
        metadata.DocumentDate ??= _clock.Today;

        _db.VehicleDocuments.Add(metadata);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return metadata.Id;
    }

    public async Task<int> AddFromFileAsync(
        string sourcePath,
        VehicleDocument metadata,
        CancellationToken cancellationToken = default)
    {
        Guard.That(File.Exists(sourcePath), $"Die Datei '{sourcePath}' wurde nicht gefunden.");

        await using var stream = File.OpenRead(sourcePath);
        return await AddAsync(stream, Path.GetFileName(sourcePath), metadata, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateMetadataAsync(VehicleDocument document, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DocumentManage);

        var existing = await _db.VehicleDocuments
            .FirstOrDefaultAsync(d => d.Id == document.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleDocument), document.Id);

        existing.Title = Guard.NotEmpty(document.Title, "Titel");
        existing.Description = document.Description;
        existing.Category = document.Category;
        existing.DocumentDate = document.DocumentDate;
        existing.ValidUntil = document.ValidUntil;
        existing.VehicleId = document.VehicleId;
        existing.RowVersion = document.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ArchiveAsync(int documentId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DocumentManage);

        var document = await _db.VehicleDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleDocument), documentId);

        // Dokumente werden archiviert, nicht geloescht - die Datei bleibt in der Ablage erhalten.
        document.IsArchived = true;
        document.ArchivedAt = _clock.Now;
        document.ArchivedByUserId = _currentUser.User?.Id;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetFullPathAsync(int documentId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DocumentView);

        var relativePath = await _db.VehicleDocuments
            .Where(d => d.Id == documentId)
            .Select(d => d.RelativePath)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleDocument), documentId);

        return _storage.GetFullPath(relativePath);
    }

    public async Task LinkToDamageAsync(
        int documentId,
        int damageId,
        string? caption,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageEdit);

        var exists = await _db.DamageAttachments
            .AnyAsync(a => a.DamageReportId == damageId && a.VehicleDocumentId == documentId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        _db.DamageAttachments.Add(new DamageAttachment
        {
            DamageReportId = damageId,
            VehicleDocumentId = documentId,
            LinkedAt = _clock.Now,
            LinkedByUserId = _currentUser.User?.Id,
            Caption = caption
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LinkToAccidentAsync(
        int documentId,
        int accidentId,
        string? caption,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentEdit);

        var exists = await _db.AccidentAttachments
            .AnyAsync(a => a.AccidentReportId == accidentId && a.VehicleDocumentId == documentId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        _db.AccidentAttachments.Add(new AccidentAttachment
        {
            AccidentReportId = accidentId,
            VehicleDocumentId = documentId,
            LinkedAt = _clock.Now,
            LinkedByUserId = _currentUser.User?.Id,
            Caption = caption
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LinkToWorkshopOrderAsync(
        int documentId,
        int orderId,
        DocumentCategory category,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        var exists = await _db.WorkshopDocuments
            .AnyAsync(a => a.WorkshopOrderId == orderId && a.VehicleDocumentId == documentId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        _db.WorkshopDocuments.Add(new WorkshopDocument
        {
            WorkshopOrderId = orderId,
            VehicleDocumentId = documentId,
            Category = category,
            LinkedAt = _clock.Now,
            LinkedByUserId = _currentUser.User?.Id
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Legt Dokumente strukturiert ab: Fahrzeuge\&lt;InterneNummer&gt;\&lt;Kategorie&gt;.</summary>
    private async Task<string> BuildTargetFolderAsync(VehicleDocument metadata, CancellationToken cancellationToken)
    {
        if (metadata.VehicleId is not { } vehicleId)
        {
            return Path.Combine("Allgemein", metadata.Category.ToString());
        }

        var internalNumber = await _db.Vehicles
            .Where(v => v.Id == vehicleId)
            .Select(v => v.InternalNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? vehicleId.ToString();

        var safeNumber = string.Concat(internalNumber.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine("Fahrzeuge", safeNumber, metadata.Category.ToString());
    }
}
