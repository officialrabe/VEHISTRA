using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.Domain.Entities;

/// <summary>
/// Metadaten eines Dokuments. Die Datei selbst liegt in der zentralen Dokumentenablage
/// auf dem Windows Server; in der Datenbank wird nur der relative Pfad gespeichert.
/// </summary>
public class VehicleDocument : AuditableEntity, IArchivable
{
    /// <summary>Null = allgemeines Dokument ohne Fahrzeugbezug.</summary>
    public int? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public DocumentCategory Category { get; set; } = DocumentCategory.Sonstiges;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Originaldateiname zum Zeitpunkt des Hochladens.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Pfad relativ zum konfigurierten Dokumentenstammverzeichnis.</summary>
    public string RelativePath { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    /// <summary>SHA-256 der Datei zur Integritaetspruefung.</summary>
    public string? Sha256 { get; set; }

    public DateTime? DocumentDate { get; set; }

    /// <summary>Ablaufdatum (z. B. Versicherungsnachweis).</summary>
    public DateTime? ValidUntil { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public int? ArchivedByUserId { get; set; }
}

/// <summary>Von der Anwendung erzeugtes Dokument (PDF-Bericht).</summary>
public class GeneratedDocument : AuditableEntity
{
    public string TemplateKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int? WorkshopOrderId { get; set; }

    public WorkshopOrder? WorkshopOrder { get; set; }

    public int? AccidentReportId { get; set; }

    public AccidentReport? AccidentReport { get; set; }

    public int? VehicleDocumentId { get; set; }

    public VehicleDocument? Document { get; set; }

    public DateTime GeneratedAt { get; set; }

    public int? GeneratedByUserId { get; set; }

    public string? GeneratedByUserName { get; set; }

    /// <summary>Kennzeichnet Blanko-Formulare ohne Datenbezug.</summary>
    public bool IsBlankForm { get; set; }
}

/// <summary>Vorlage fuer erzeugte Dokumente (Bezeichnung, Kopfzeile, Hinweise).</summary>
public class DocumentTemplate : AuditableEntity
{
    /// <summary>Eindeutiger Schluessel, z. B. WORKSHOP_REPORT oder ACCIDENT_REPORT.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Optionaler Kopfzeilentext; leer = Unternehmensname aus den Einstellungen.</summary>
    public string? HeaderText { get; set; }

    public string? FooterText { get; set; }

    /// <summary>Zusaetzlicher Hinweistext, der im Bericht ausgegeben wird.</summary>
    public string? NoticeText { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystemTemplate { get; set; }
}
