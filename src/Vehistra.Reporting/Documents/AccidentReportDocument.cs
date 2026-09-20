using Vehistra.Application.Abstractions;
using Vehistra.Domain.Enums;
using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vehistra.Reporting.Documents;

/// <summary>
/// Unfall- und Schadensbericht auf zwei Seiten DIN A4 hoch.
/// Seite 1: Angaben zum Ereignis. Seite 2: Unfallskizze mit Raster und Fahrzeugschema.
/// </summary>
internal sealed class AccidentReportDocument : IDocument
{
    /// <summary>Schadensarten des Formulars in der Reihenfolge der Vorlage.</summary>
    private static readonly (string Key, string Label)[] DamageKinds =
    [
        (nameof(AccidentType.UnfallMitFremdbeteiligung), "Unfall mit Fremdbeteiligung"),
        (nameof(AccidentType.UnfallOhneFremdbeteiligung), "Unfall ohne Fremdbeteiligung"),
        (nameof(AccidentType.ParkschadenVerursacherUnbekannt), "Parkschaden – Verursacher unbekannt"),
        (nameof(AccidentType.Wildschaden), "Wildschaden"),
        (nameof(AccidentType.Glasbruch), "Glasbruch"),
        (nameof(AccidentType.DiebstahlEinbruch), "Diebstahl / Einbruch"),
        (nameof(AccidentType.Vandalismus), "Vandalismus"),
        (nameof(AccidentType.SturmHagelUnwetter), "Sturm / Hagel / Unwetter"),
        (nameof(AccidentType.TechnischerDefekt), "Technischer Defekt"),
        (nameof(AccidentType.Sonstiges), "Sonstiges")
    ];

    private const string SketchHint =
        "In die Skizze gehören: Straßenverlauf · Straßennamen · Fahrbahnmarkierungen · Verkehrszeichen · Ampeln · " +
        "Standorte der beteiligten Fahrzeuge · Fahrtrichtungen · eigenes Fahrzeug mit \"1\" · Aufprallstelle mit \"X\" · " +
        "Endpositionen · Bremsspuren · Sichthindernisse.";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly AccidentReportData _data;

    public AccidentReportDocument(AccidentReportData data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = "Unfall- / Schadensbericht",
        Author = _data.Header.CompanyName,
        Subject = "Unfall- und Schadensmeldung",
        Creator = "Vehistra - LSP Virtual Services",
        Producer = "Vehistra"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            ConfigurePage(page);

            page.Header().Element(header => ReportComponents.Header(
                header, _data.Header, "UNFALL- / SCHADENSBERICHT",
                "Vom Fahrer auszufüllen", "Seite 1 von 2"));

            page.Content().PaddingTop(5).Element(FirstPage);
            page.Footer().Element(footer => ReportComponents.Footer(footer, _data.Header, _data.IsBlankForm));
        });

        container.Page(page =>
        {
            ConfigurePage(page);

            page.Header().Element(header => ReportComponents.Header(
                header, _data.Header, "UNFALLSKIZZE",
                "Gehört zum Unfall- / Schadensbericht", "Seite 2 von 2"));

            page.Content().PaddingTop(5).Element(SecondPage);
            page.Footer().Element(footer => ReportComponents.Footer(footer, _data.Header, _data.IsBlankForm));
        });
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(14, Unit.Millimetre);
        page.DefaultTextStyle(text => text
            .FontFamily(ReportStyles.FontFamily)
            .FontSize(ReportStyles.BaseFontSize)
            .FontColor(Colors.Black));
    }

    private void FirstPage(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(6);

            // Fahrzeug und Fahrer
            column.Item().Element(c => ReportComponents.SectionTitle(c, "Fahrzeug und Fahrer"));

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Kennzeichen", _data.LicensePlate));
                row.RelativeItem(2).Element(c => ReportComponents.Field(c, "Interne Nr.", _data.InternalNumber));
                row.RelativeItem(4).Element(c => ReportComponents.Field(
                    c, "Hersteller / Modell", _data.VehicleDescription));
                row.RelativeItem(2).Element(c => ReportComponents.Field(
                    c, "Kilometerstand", _data.Mileage?.ToString("N0", German)));
            });

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(4).Element(c => ReportComponents.Field(c, "Fahrer", _data.DriverName));
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Telefon", _data.DriverPhone));
                row.RelativeItem(3).Element(c => ReportComponents.Field(
                    c, "Schadensnummer", _data.AccidentNumber));
            });

            // Zeitpunkt und Ort
            column.Item().PaddingTop(1).Element(c => ReportComponents.SectionTitle(c, "Zeitpunkt und Ort"));

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(2).Element(c => ReportComponents.Field(
                    c, "Datum", _data.OccurredAt?.ToString("dd.MM.yyyy")));
                row.RelativeItem(2).Element(c => ReportComponents.Field(
                    c, "Uhrzeit", _data.OccurredAt?.ToString("HH:mm")));
                row.RelativeItem(6).Element(c => ReportComponents.Field(c, "Ort / Straße", _data.Location));
            });

            // Art des Schadens
            column.Item().PaddingTop(1).Element(c => ReportComponents.SectionTitle(c, "Art des Schadens"));

            column.Item().Border(0.8f).BorderColor(ReportStyles.LineColor).BorderTop(0).Padding(5).Column(kinds =>
            {
                var rows = (int)Math.Ceiling(DamageKinds.Length / 2.0);

                for (var rowIndex = 0; rowIndex < rows; rowIndex++)
                {
                    kinds.Item().PaddingVertical(1.5f).Row(row =>
                    {
                        row.Spacing(8);

                        for (var columnIndex = 0; columnIndex < 2; columnIndex++)
                        {
                            var index = rowIndex * 2 + columnIndex;

                            if (index >= DamageKinds.Length)
                            {
                                row.RelativeItem();
                                continue;
                            }

                            var kind = DamageKinds[index];
                            row.RelativeItem().Element(c => ReportComponents.CheckBox(
                                c, kind.Label,
                                string.Equals(_data.DamageKindKey, kind.Key, StringComparison.OrdinalIgnoreCase)));
                        }
                    });
                }
            });

            // Hergang
            column.Item().PaddingTop(1).Element(c => ReportComponents.SectionTitle(c, "Hergang"));
            column.Item().Element(c => ReportComponents.WritingLines(c, 5, _data.CourseOfEvents));

            // Beteiligter Dritter
            column.Item().PaddingTop(1).Element(c => ReportComponents.SectionTitle(c, "Beteiligter Dritter"));

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(3).Element(c => ReportComponents.Field(
                    c, "Kennzeichen", _data.Participant?.LicensePlate));
                row.RelativeItem(5).Element(c => ReportComponents.Field(
                    c, "Name, Vorname", _data.Participant?.Name));
                row.RelativeItem(3).Element(c => ReportComponents.Field(
                    c, "Telefon", _data.Participant?.Phone));
            });

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(5).Element(c => ReportComponents.Field(
                    c, "Anschrift", _data.Participant?.Address));
                row.RelativeItem(4).Element(c => ReportComponents.Field(
                    c, "Versicherung", _data.Participant?.InsuranceCompany));
                row.RelativeItem(3).Element(c => ReportComponents.Field(
                    c, "Versicherungsnummer", _data.Participant?.InsuranceNumber));
            });

            // Polizei und Zeugen
            column.Item().PaddingTop(1).Element(c => ReportComponents.SectionTitle(c, "Polizei und Zeugen"));

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem().Element(c => ReportComponents.YesNo(c, "Polizei aufgenommen", _data.PoliceInvolved));
                row.RelativeItem().Element(c => ReportComponents.YesNo(c, "Personenschaden", _data.PersonalInjury));
                row.RelativeItem().Element(c => ReportComponents.YesNo(c, "Fahrzeug fahrbereit", _data.VehicleDriveable));
            });

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(4).Element(c => ReportComponents.Field(c, "Dienststelle", _data.PoliceStation));
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Tagebuchnummer", _data.PoliceFileNumber));
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Zeuge Name", _data.WitnessName));
                row.RelativeItem(2).Element(c => ReportComponents.Field(c, "Zeuge Telefon", _data.WitnessPhone));
            });

            // Hinweis
            column.Item().PaddingTop(3).Background(ReportStyles.FieldBackground)
                .Border(0.8f).BorderColor(Colors.Black).Padding(5).Column(notice =>
                {
                    notice.Item().Text("WICHTIGER HINWEIS").FontSize(7.5f).Bold().LetterSpacing(0.08f);
                    notice.Item().PaddingTop(1).Text(
                        "Kein Schuldanerkenntnis abgeben. Angaben wahrheitsgemäß und vollständig ausfüllen. " +
                        "Die Unfallskizze auf Seite 2 gehört zum Bericht.")
                        .FontSize(ReportStyles.SmallFontSize);

                    if (!string.IsNullOrWhiteSpace(_data.Notice))
                    {
                        notice.Item().PaddingTop(2).Text(_data.Notice).FontSize(ReportStyles.SmallFontSize);
                    }
                });

            // Unterschriften
            column.Item().PaddingTop(5).Row(row =>
            {
                row.Spacing(18);
                row.RelativeItem().Element(c => ReportComponents.SignatureField(c, "Datum / Unterschrift Fahrer"));
                row.RelativeItem().Element(c => ReportComponents.SignatureField(
                    c, "Aufgenommen von / Fuhrpark"));
            });
        });
    }

    private void SecondPage(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(6);

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Kennzeichen", _data.LicensePlate));
                row.RelativeItem(3).Element(c => ReportComponents.Field(
                    c, "Datum des Schadens", _data.OccurredAt?.ToString("dd.MM.yyyy")));
                row.RelativeItem(4).Element(c => ReportComponents.Field(c, "Fahrer", _data.DriverName));
            });

            column.Item().Element(c => ReportComponents.SectionTitle(c, "Skizze"));

            // Rasterbereich fuer die handschriftliche Skizze
            column.Item().Border(1f).BorderColor(Colors.Black).Height(270).Svg(BuildGridSvg);

            column.Item().Background(ReportStyles.FieldBackground)
                .Border(0.6f).BorderColor(ReportStyles.LineColor).Padding(4)
                .Text(SketchHint).FontSize(ReportStyles.SmallFontSize);

            column.Item().Element(c => ReportComponents.SectionTitle(c, "Schäden am Fahrzeug"));

            column.Item().Border(0.8f).BorderColor(ReportStyles.LineColor).BorderTop(0)
                .Padding(6).Row(row =>
                {
                    row.RelativeItem().Height(150).Column(vehicle =>
                    {
                        vehicle.Item().Height(11).AlignCenter()
                            .Text("VORNE").FontSize(7.5f).SemiBold();

                        vehicle.Item().Height(126).Row(inner =>
                        {
                            inner.ConstantItem(34).AlignMiddle().AlignRight().PaddingRight(3)
                                .Text("LINKS").FontSize(7.5f).SemiBold();

                            inner.RelativeItem().Svg(BuildVehicleOutlineSvg);

                            inner.ConstantItem(34).AlignMiddle().PaddingLeft(3)
                                .Text("RECHTS").FontSize(7.5f).SemiBold();
                        });

                        vehicle.Item().Height(11).AlignCenter()
                            .Text("HINTEN").FontSize(7.5f).SemiBold();
                    });

                    row.ConstantItem(170).PaddingLeft(8).Column(hints =>
                    {
                        hints.Item().Text("Beschädigte Stellen bitte einkreisen.")
                            .FontSize(ReportStyles.SmallFontSize).SemiBold();

                        hints.Item().PaddingTop(4).Text(
                            "Fotos von Unfallstelle, Fahrzeugen und Beschädigungen aufnehmen und dem " +
                            "Fuhrpark zusammen mit diesem Bericht übergeben.")
                            .FontSize(ReportStyles.SmallFontSize);

                        hints.Item().PaddingTop(10).Element(c => ReportComponents.Field(
                            c, "Weitere Bemerkungen", null));
                        hints.Item().PaddingTop(4).Element(c => ReportComponents.Field(c, string.Empty, null));
                    });
                });

            column.Item().PaddingTop(4).Row(row =>
            {
                row.Spacing(18);
                row.RelativeItem().Element(c => ReportComponents.SignatureField(c, "Datum / Unterschrift Fahrer"));
                row.RelativeItem();
            });
        });
    }

    /// <summary>Erzeugt das Raster des Skizzenbereichs als Vektorgrafik.</summary>
    private static string BuildGridSvg(Size size)
    {
        const float step = 14f;
        var builder = new StringBuilder();

        builder.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size.Width:0.##}\" height=\"{size.Height:0.##}\" " +
            $"viewBox=\"0 0 {size.Width:0.##} {size.Height:0.##}\">");

        var index = 0;
        for (var x = step; x < size.Width; x += step)
        {
            var isMajor = ++index % 5 == 0;
            builder.Append(CultureInfo.InvariantCulture,
                $"<line x1=\"{x:0.##}\" y1=\"0\" x2=\"{x:0.##}\" y2=\"{size.Height:0.##}\" " +
                $"stroke=\"{(isMajor ? "#969696" : "#c8c8c8")}\" stroke-width=\"{(isMajor ? "0.8" : "0.4")}\" />");
        }

        index = 0;
        for (var y = step; y < size.Height; y += step)
        {
            var isMajor = ++index % 5 == 0;
            builder.Append(CultureInfo.InvariantCulture,
                $"<line x1=\"0\" y1=\"{y:0.##}\" x2=\"{size.Width:0.##}\" y2=\"{y:0.##}\" " +
                $"stroke=\"{(isMajor ? "#969696" : "#c8c8c8")}\" stroke-width=\"{(isMajor ? "0.8" : "0.4")}\" />");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    /// <summary>
    /// Erzeugt das Fahrzeugschema (Draufsicht) als Vektorgrafik.
    /// Die Beschriftung VORNE / HINTEN / LINKS / RECHTS wird im Bericht daneben gesetzt.
    /// </summary>
    private static string BuildVehicleOutlineSvg(Size size)
    {
        var width = size.Width;
        var height = size.Height;

        var bodyWidth = Math.Min(74f, width * 0.5f);
        var bodyHeight = height - 6f;
        var left = (width - bodyWidth) / 2f;
        const float top = 3f;

        const string black = "#000000";
        const string grey = "#646464";
        var builder = new StringBuilder();

        builder.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width:0.##}\" height=\"{height:0.##}\" " +
            $"viewBox=\"0 0 {width:0.##} {height:0.##}\" font-family=\"sans-serif\" font-size=\"7.5\">");

        // Fahrzeugumriss
        builder.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"{left:0.##}\" y=\"{top:0.##}\" width=\"{bodyWidth:0.##}\" height=\"{bodyHeight:0.##}\" " +
            $"rx=\"12\" ry=\"12\" fill=\"none\" stroke=\"{black}\" stroke-width=\"1.2\" />");

        // Windschutz- und Heckscheibe
        foreach (var factor in new[] { 0.22f, 0.78f })
        {
            var y = top + bodyHeight * factor;
            builder.Append(CultureInfo.InvariantCulture,
                $"<line x1=\"{left + 6:0.##}\" y1=\"{y:0.##}\" x2=\"{left + bodyWidth - 6:0.##}\" y2=\"{y:0.##}\" " +
                $"stroke=\"{grey}\" stroke-width=\"0.8\" />");
        }

        // Fahrgastzelle
        builder.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"{left + 8:0.##}\" y=\"{top + bodyHeight * 0.3f:0.##}\" " +
            $"width=\"{bodyWidth - 16:0.##}\" height=\"{bodyHeight * 0.4f:0.##}\" rx=\"6\" ry=\"6\" " +
            $"fill=\"none\" stroke=\"{grey}\" stroke-width=\"0.8\" />");

        // Raeder
        const float wheelWidth = 5f;
        const float wheelHeight = 14f;

        foreach (var wheelTop in new[] { top + bodyHeight * 0.14f, top + bodyHeight * 0.72f })
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{left - wheelWidth:0.##}\" y=\"{wheelTop:0.##}\" width=\"{wheelWidth:0.##}\" " +
                $"height=\"{wheelHeight:0.##}\" fill=\"none\" stroke=\"{black}\" stroke-width=\"1.2\" />");

            builder.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{left + bodyWidth:0.##}\" y=\"{wheelTop:0.##}\" width=\"{wheelWidth:0.##}\" " +
                $"height=\"{wheelHeight:0.##}\" fill=\"none\" stroke=\"{black}\" stroke-width=\"1.2\" />");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }
}
