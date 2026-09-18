using System.Text;
using Vehistra.Application.Abstractions;
using Vehistra.Reporting;

namespace Vehistra.UnitTests;

/// <summary>
/// Tests der PDF-Berichte. Geprueft werden Format, Seitenzahl, Inhalt und die Zusage,
/// dass die Fusszeile keine Internetadressen enthaelt.
/// </summary>
public class ReportServiceTests
{
    private readonly IReportService _reports = new QuestPdfReportService();

    // Der Abbruchtoken des Testlaufs wird an jeden Aufruf durchgereicht.
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<byte[]> Workshop(WorkshopReportData data) =>
        _reports.CreateWorkshopReportAsync(data, Token);

    private Task<byte[]> Accident(AccidentReportData data) =>
        _reports.CreateAccidentReportAsync(data, Token);

    private Task<byte[]> VehicleFile(VehicleFileReportData data) =>
        _reports.CreateVehicleFileReportAsync(data, Token);

    private Task<byte[]> Table(TableReportData data) =>
        _reports.CreateTableReportAsync(data, Token);

    private static ReportHeaderData Header() => new()
    {
        CompanyName = "Musterbetrieb GmbH",
        CompanyAddress = "Musterweg 1, 12345 Musterstadt",
        PrintedAt = new DateTime(2026, 3, 14, 9, 30, 0),
        PrintedBy = "M. Mustermann",
        ApplicationVersion = "1.0.0"
    };

    // ---------------------------------------------------------------- Werkstatt

    [Fact]
    public async Task Werkstattbericht_erzeugt_ein_gueltiges_PDF()
    {
        var pdf = await Workshop(new WorkshopReportData { Header = Header() });

        pdf.ShouldNotBeEmpty();
        IsPdf(pdf).ShouldBeTrue();
    }

    [Fact]
    public async Task Werkstattbericht_passt_auf_ein_Blatt_DIN_A4()
    {
        var data = new WorkshopReportData
        {
            Header = Header(),
            LicensePlate = "FDS-AB 123",
            InternalNumber = "T-07",
            VehicleDescription = "Mercedes-Benz E-Klasse",
            DriverName = "Anna Beispiel",
            Mileage = 184_500,
            Date = new DateTime(2026, 3, 14),
            CheckedStandardTasks = new HashSet<string>
            {
                WorkshopStandardTasks.OilChange,
                WorkshopStandardTasks.Brakes,
                WorkshopStandardTasks.Hu
            },
            TaskLines =
            [
                new WorkshopReportTaskLine(1, "Bremsbelaege vorne erneuern", false),
                new WorkshopReportTaskLine(2, "Oelwechsel inklusive Filter", true)
            ]
        };

        var pdf = await Workshop(data);

        CountPages(pdf).ShouldBe(1);
    }

    [Fact]
    public async Task Werkstattbericht_als_Blankoformular_bleibt_einseitig()
    {
        var pdf = await Workshop(
            new WorkshopReportData { Header = Header(), IsBlankForm = true });

        CountPages(pdf).ShouldBe(1);
    }

    [Fact]
    public async Task Werkstattbericht_enthaelt_alle_zwoelf_Standardarbeiten()
    {
        var pdf = await Workshop(
            new WorkshopReportData { Header = Header(), IsBlankForm = true });

        var text = ExtractText(pdf);

        foreach (var (_, label) in WorkshopStandardTasks.All)
        {
            // Nur der erste Wortteil, damit Zeilenumbrueche im PDF nicht stoeren.
            text.ShouldContain(label.Split(' ')[0]);
        }
    }

    [Fact]
    public async Task Werkstattbericht_uebernimmt_die_Fahrzeugangaben()
    {
        var pdf = await Workshop(new WorkshopReportData
        {
            Header = Header(),
            LicensePlate = "FDS-AB 123",
            InternalNumber = "T-07",
            Mileage = 184_500
        });

        var text = ExtractText(pdf);

        text.ShouldContain("FDS-AB 123");
        text.ShouldContain("T-07");
        text.ShouldContain("184.500");
    }

    // ---------------------------------------------------------------- Unfall

    [Fact]
    public async Task Unfallbericht_umfasst_genau_zwei_Blaetter()
    {
        var pdf = await Accident(new AccidentReportData
        {
            Header = Header(),
            LicensePlate = "FDS-AB 123",
            OccurredAt = new DateTime(2026, 3, 12, 14, 20, 0),
            Location = "Kreuzung Hauptstraße / Bahnhofstraße"
        });

        CountPages(pdf).ShouldBe(2);
    }

    [Fact]
    public async Task Unfallbericht_als_Blankoformular_umfasst_ebenfalls_zwei_Blaetter()
    {
        var pdf = await Accident(
            new AccidentReportData { Header = Header(), IsBlankForm = true });

        CountPages(pdf).ShouldBe(2);
    }

    [Fact]
    public async Task Unfallbericht_weist_auf_das_fehlende_Schuldanerkenntnis_hin()
    {
        var pdf = await Accident(
            new AccidentReportData { Header = Header(), IsBlankForm = true });

        ExtractText(pdf).ShouldContain("Schuldanerkenntnis");
    }

    [Fact]
    public async Task Unfallbericht_beschriftet_die_Fahrzeugskizze()
    {
        var pdf = await Accident(
            new AccidentReportData { Header = Header(), IsBlankForm = true });

        var text = ExtractText(pdf);

        text.ShouldContain("VORNE");
        text.ShouldContain("HINTEN");
        text.ShouldContain("LINKS");
        text.ShouldContain("RECHTS");
    }

    [Fact]
    public async Task Unfallbericht_uebernimmt_den_Unfallgegner()
    {
        var pdf = await Accident(new AccidentReportData
        {
            Header = Header(),
            Participant = new AccidentReportParticipant
            {
                Name = "Peter Gegner",
                LicensePlate = "S-XY 999",
                InsuranceCompany = "Beispielversicherung AG"
            }
        });

        var text = ExtractText(pdf);

        text.ShouldContain("Peter Gegner");
        text.ShouldContain("S-XY 999");
    }

    // ---------------------------------------------------------------- Weitere

    [Fact]
    public async Task Fahrzeugakte_enthaelt_Stammdaten_und_Abschnitte()
    {
        var pdf = await VehicleFile(new VehicleFileReportData
        {
            Header = Header(),
            VehicleDisplay = "FDS-AB 123 · Mercedes-Benz E-Klasse",
            MasterData =
            [
                ("Kennzeichen", "FDS-AB 123"),
                ("Fahrgestellnummer", "WDD1234567A123456")
            ],
            Sections =
            [
                ("Hauptuntersuchungen",
                 new[] { "Datum", "Ergebnis" },
                 new IReadOnlyList<string?>[] { new string?[] { "14.03.2026", "ohne Mängel" } })
            ]
        });

        var text = ExtractText(pdf);

        IsPdf(pdf).ShouldBeTrue();
        text.ShouldContain("WDD1234567A123456");
        text.ShouldContain("Hauptuntersuchungen");
    }

    [Fact]
    public async Task Listenbericht_gibt_alle_Zeilen_aus()
    {
        var rows = Enumerable.Range(1, 40)
            .Select(i => (IReadOnlyList<string?>)new string?[] { $"FDS-AB {i}", $"Fahrzeug {i}" })
            .ToList();

        var pdf = await Table(new TableReportData
        {
            Header = Header(),
            Title = "Fahrzeugliste",
            Columns = ["Kennzeichen", "Bezeichnung"],
            Rows = rows
        });

        var text = ExtractText(pdf);

        text.ShouldContain("Fahrzeug 1");
        text.ShouldContain("Fahrzeug 40");
    }

    // ---------------------------------------------------------------- Zusagen

    [Fact]
    public async Task Kein_Bericht_enthaelt_eine_Internetadresse_in_der_Fusszeile()
    {
        var berichte = new[]
        {
            await Workshop(new WorkshopReportData { Header = Header() }),
            await Accident(new AccidentReportData { Header = Header() })
        };

        foreach (var pdf in berichte)
        {
            var text = ExtractText(pdf);

            text.ShouldNotContain("localhost");
            text.ShouldNotContain("127.0.0.1");
            text.ShouldNotContain("http://");
            text.ShouldNotContain("https://");
        }
    }

    [Fact]
    public async Task Jeder_Bericht_nennt_den_konfigurierten_Firmennamen()
    {
        var pdf = await Workshop(new WorkshopReportData { Header = Header() });

        var text = ExtractText(pdf);

        text.ShouldContain("Musterbetrieb");
        // Der Firmenname steht nirgends fest im Programm.
        text.ShouldNotContain("Taxi Schumacher");
    }

    // ---------------------------------------------------------------- Hilfen

    private static bool IsPdf(byte[] pdf) =>
        pdf.Length > 4 && Encoding.ASCII.GetString(pdf, 0, 5) == "%PDF-";

    /// <summary>Zaehlt die Seiten anhand der Seitenobjekte im PDF.</summary>
    private static int CountPages(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var count = 0;
        var index = 0;

        while ((index = raw.IndexOf("/Type /Page", index, StringComparison.Ordinal)) >= 0)
        {
            // "/Type /Pages" (der Sammelknoten) zaehlt nicht mit.
            if (index + 11 >= raw.Length || raw[index + 11] != 's')
            {
                count++;
            }

            index += 11;
        }

        return count;
    }

    /// <summary>
    /// Liest die im PDF sichtbaren Zeichenketten. Ausgewertet werden die
    /// Textanweisungen der unkomprimierten Inhaltsstroeme.
    /// </summary>
    private static string ExtractText(byte[] pdf) => PdfTextReader.Extract(pdf);
}
