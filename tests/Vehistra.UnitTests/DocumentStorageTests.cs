using Microsoft.Extensions.Logging.Abstractions;
using Vehistra.Infrastructure.Storage;

namespace Vehistra.UnitTests;

/// <summary>
/// Die Dokumentenablage darf sich nicht auf die Dateiendung verlassen. Eine
/// umbenannte EXE ist der Fall, um den es geht: sie landet in einem Ordner, auf
/// den mehrere Arbeitsplätze zugreifen, und wird dort irgendwann angeklickt.
/// </summary>
public class DocumentStorageTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "vehistra-ablage-" + Guid.NewGuid().ToString("N"))).FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private FileSystemDocumentStorage Ablage()
    {
        var storage = new FileSystemDocumentStorage(NullLogger<FileSystemDocumentStorage>.Instance);
        storage.Configure(_root);
        return storage;
    }

    private static MemoryStream Inhalt(params byte[] bytes) => new(bytes);

    private static MemoryStream Text(string text) =>
        new(System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>Gueltiges PDF: Signatur %PDF- am Anfang.</summary>
    private static MemoryStream Pdf() => Text("%PDF-1.7\nInhalt");

    [Fact]
    public async Task Ein_echtes_PDF_wird_abgelegt()
    {
        var ergebnis = await Ablage().StoreAsync(Pdf(), "Rechnung.pdf", "Fahrzeuge/T-01", Token);

        ergebnis.RelativePath.ShouldContain("Rechnung.pdf");
        ergebnis.SizeBytes.ShouldBeGreaterThan(0);
        File.Exists(Path.Combine(_root, ergebnis.RelativePath)).ShouldBeTrue();
    }

    [Fact]
    public async Task Eine_umbenannte_EXE_wird_abgelehnt()
    {
        // "MZ" ist der Anfang jeder Windows-EXE.
        var fehler = await Should.ThrowAsync<InvalidOperationException>(() =>
            Ablage().StoreAsync(Inhalt(0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00),
                "Rechnung.pdf", "Fahrzeuge/T-01", Token));

        fehler.Message.ShouldContain("ausfuehrbaren Inhalt");

        // Und sie darf nicht in der Ablage liegen bleiben.
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Auch_hinter_einer_harmlosen_Endung_wird_ein_Programm_erkannt()
    {
        var fehler = await Should.ThrowAsync<InvalidOperationException>(() =>
            Ablage().StoreAsync(Inhalt(0x4D, 0x5A, 0x00, 0x00), "notiz.txt", "Fahrzeuge/T-01", Token));

        fehler.Message.ShouldContain("ausfuehrbaren Inhalt");
    }

    [Fact]
    public async Task Ein_Linuxprogramm_wird_ebenfalls_erkannt()
    {
        var fehler = await Should.ThrowAsync<InvalidOperationException>(() =>
            Ablage().StoreAsync(Inhalt(0x7F, 0x45, 0x4C, 0x46, 0x02), "anleitung.pdf", "Fahrzeuge/T-01", Token));

        fehler.Message.ShouldContain("ausfuehrbaren Inhalt");
    }

    [Fact]
    public async Task Ein_Inhalt_der_nicht_zur_Endung_passt_wird_abgelehnt()
    {
        // PNG-Signatur, aber .pdf als Endung.
        var fehler = await Should.ThrowAsync<InvalidOperationException>(() =>
            Ablage().StoreAsync(Inhalt(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
                "bild.pdf", "Fahrzeuge/T-01", Token));

        fehler.Message.ShouldContain("passt nicht zur Endung");
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Ein_echtes_PNG_mit_passender_Endung_wird_abgelegt()
    {
        var ergebnis = await Ablage().StoreAsync(
            Inhalt(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01),
            "schaden.png", "Fahrzeuge/T-01", Token);

        ergebnis.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task Eine_Textdatei_ohne_Signatur_wird_abgelegt()
    {
        // .txt und .csv haben keine verlaessliche Signatur - hier darf die
        // Pruefung nicht im Weg stehen.
        var ergebnis = await Ablage().StoreAsync(
            Text("Kennzeichen;Fahrer\nFDS-AB 123;Mustermann"), "liste.csv", "Import", Token);

        ergebnis.SizeBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Eine_Officedatei_wird_als_ZIP_Behaelter_erkannt()
    {
        var ergebnis = await Ablage().StoreAsync(
            Inhalt(0x50, 0x4B, 0x03, 0x04, 0x14, 0x00), "Vertrag.docx", "Fahrzeuge/T-01", Token);

        ergebnis.SizeBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Eine_leere_Datei_wird_abgelehnt()
    {
        var fehler = await Should.ThrowAsync<InvalidOperationException>(() =>
            Ablage().StoreAsync(new MemoryStream(), "leer.pdf", "Fahrzeuge/T-01", Token));

        fehler.Message.ShouldContain("leer");
    }

    [Fact]
    public async Task Eine_ausfuehrbare_Endung_wird_gar_nicht_erst_angenommen()
    {
        var fehler = await Should.ThrowAsync<InvalidOperationException>(() =>
            Ablage().StoreAsync(Pdf(), "programm.exe", "Fahrzeuge/T-01", Token));

        fehler.Message.ShouldContain("nicht zugelassen");
    }

    [Fact]
    public async Task Ein_Zielordner_mit_Punktpunkt_landet_trotzdem_in_der_Ablage()
    {
        // "../" wird entfernt, nicht abgewiesen. Entscheidend ist, dass die
        // Datei die Ablage nicht verlaesst.
        var ergebnis = await Ablage().StoreAsync(Pdf(), "Rechnung.pdf", "../../ausserhalb", Token);

        var vollstaendig = Path.GetFullPath(Path.Combine(_root, ergebnis.RelativePath));

        vollstaendig.ShouldStartWith(Path.GetFullPath(_root));
        File.Exists(vollstaendig).ShouldBeTrue();

        // Oberhalb der Ablage darf nichts entstanden sein.
        var darueber = Path.GetFullPath(Path.Combine(_root, ".."));
        Directory.GetDirectories(darueber, "ausserhalb", SearchOption.TopDirectoryOnly).ShouldBeEmpty();
    }

    [Fact]
    public async Task Ein_Dateiname_mit_Pfadanteil_wird_entschaerft()
    {
        var ergebnis = await Ablage().StoreAsync(Pdf(), "..\\..\\Rechnung.pdf", "Fahrzeuge/T-01", Token);

        var vollstaendig = Path.GetFullPath(Path.Combine(_root, ergebnis.RelativePath));

        vollstaendig.ShouldStartWith(Path.GetFullPath(_root));
    }
}
