using System.Globalization;
using Vehistra.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vehistra.Reporting.Documents;

/// <summary>
/// Werkstattbericht auf einer Seite DIN A4 hoch. Vektorbasiert, schwarz/weiss gut druckbar.
/// Ohne Daten entsteht ein vollstaendig leeres Blankoformular.
/// </summary>
internal sealed class WorkshopReportDocument : IDocument
{
    private const int TaskLineCount = 15;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly WorkshopReportData _data;

    public WorkshopReportDocument(WorkshopReportData data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = "Werkstattbericht",
        Author = _data.Header.CompanyName,
        Subject = "Auftrag an die Werkstatt",
        Creator = "Vehistra - LSP Virtual Services",
        Producer = "Vehistra"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(14, Unit.Millimetre);
            page.DefaultTextStyle(text => text
                .FontFamily(ReportStyles.FontFamily)
                .FontSize(ReportStyles.BaseFontSize)
                .FontColor(Colors.Black));

            page.Header().Element(header => ReportComponents.Header(
                header, _data.Header, "WERKSTATTBERICHT", "Auftrag an die Werkstatt",
                _data.OrderNumber is null ? null : $"Vorgang {_data.OrderNumber}"));

            page.Content().PaddingTop(6).Element(Content);

            page.Footer().Element(footer => ReportComponents.Footer(footer, _data.Header, _data.IsBlankForm));
        });
    }

    private void Content(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(7);

            // Fahrzeug und Fahrer
            column.Item().Element(c => ReportComponents.SectionTitle(c, "Fahrzeug und Fahrer"));

            column.Item().PaddingTop(2).Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Kennzeichen", _data.LicensePlate));
                row.RelativeItem(2).Element(c => ReportComponents.Field(c, "Interne Nr.", _data.InternalNumber));
                row.RelativeItem(4).Element(c => ReportComponents.Field(c, "Fahrzeug", _data.VehicleDescription));
                row.RelativeItem(2).Element(c => ReportComponents.Field(
                    c, "Kilometerstand", _data.Mileage?.ToString("N0", German)));
            });

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Fahrer", _data.DriverName));
                row.RelativeItem(2).Element(c => ReportComponents.Field(c, "Telefon", _data.DriverPhone));
                row.RelativeItem(2).Element(c => ReportComponents.Field(
                    c, "Datum", _data.Date?.ToString("dd.MM.yyyy")));
                row.RelativeItem(2).Element(c => ReportComponents.Field(
                    c, "Terminvorschlag Fahrer", _data.ProposedAppointment?.ToString("dd.MM.yyyy")));
                row.RelativeItem(3).Element(c => ReportComponents.Field(c, "Werkstatt", _data.WorkshopName));
            });

            // Haeufige Arbeiten
            column.Item().PaddingTop(2).Element(c => ReportComponents.SectionTitle(c, "Häufige Arbeiten"));

            column.Item().Border(0.8f).BorderColor(ReportStyles.LineColor).BorderTop(0).Padding(5).Column(tasks =>
            {
                var entries = WorkshopStandardTasks.All;
                var rows = (int)Math.Ceiling(entries.Count / 3.0);

                for (var rowIndex = 0; rowIndex < rows; rowIndex++)
                {
                    tasks.Item().PaddingVertical(1.5f).Row(row =>
                    {
                        row.Spacing(6);

                        for (var columnIndex = 0; columnIndex < 3; columnIndex++)
                        {
                            var entryIndex = rowIndex * 3 + columnIndex;

                            if (entryIndex >= entries.Count)
                            {
                                row.RelativeItem();
                                continue;
                            }

                            var entry = entries[entryIndex];
                            row.RelativeItem().Element(c => ReportComponents.CheckBox(
                                c, entry.Label, _data.CheckedStandardTasks.Contains(entry.Key)));
                        }
                    });
                }
            });

            // Schaeden / auszufuehrende Arbeiten
            column.Item().PaddingTop(2).Element(c => ReportComponents.SectionTitle(
                c, "Schäden / auszuführende Arbeiten"));

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.RelativeColumn();
                    columns.ConstantColumn(54);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Nr.");
                    header.Cell().Element(HeaderCell).Text("Beschreibung");
                    header.Cell().Element(HeaderCell).AlignCenter().Text("Erledigt");
                });

                for (var i = 1; i <= TaskLineCount; i++)
                {
                    var line = _data.TaskLines.FirstOrDefault(t => t.Position == i);

                    table.Cell().Element(BodyCell).AlignCenter().Text(i.ToString())
                        .Style(ReportStyles.Small(TextStyle.Default));

                    table.Cell().Element(BodyCell).Text(line?.Description ?? string.Empty)
                        .FontSize(ReportStyles.BaseFontSize - 0.5f);

                    table.Cell().Element(BodyCell).AlignCenter().AlignMiddle()
                        .Element(c => CheckCell(c, line?.IsCompleted == true));
                }
            });

            // Rueckmeldung der Werkstatt
            column.Item().PaddingTop(2).Element(c => ReportComponents.SectionTitle(
                c, "Rückmeldung der Werkstatt"));

            column.Item().PaddingTop(2).Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem().Element(c => ReportComponents.Field(c, "Kosten netto (EUR)", null));
                row.RelativeItem().Element(c => ReportComponents.Field(c, "Fahrzeug fertig am", null));
                row.RelativeItem().Element(c => ReportComponents.Field(c, "Nächster Service bei km", null));
                row.RelativeItem().Element(c => ReportComponents.Field(c, "Rechnungsnummer", null));
            });

            if (!string.IsNullOrWhiteSpace(_data.Notice))
            {
                column.Item().PaddingTop(2).Background(ReportStyles.FieldBackground)
                    .Border(0.6f).BorderColor(ReportStyles.LineColor).Padding(4)
                    .Text(_data.Notice).Style(ReportStyles.Small(TextStyle.Default));
            }

            // Unterschriften
            column.Item().PaddingTop(6).Row(row =>
            {
                row.Spacing(18);
                row.RelativeItem().Element(c => ReportComponents.SignatureField(
                    c, "Datum / Unterschrift Fahrer bzw. Fuhrpark"));
                row.RelativeItem().Element(c => ReportComponents.SignatureField(
                    c, "Datum / Unterschrift Werkstatt"));
            });
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Border(0.8f).BorderColor(ReportStyles.LineColor)
            .Background(ReportStyles.SectionBackground)
            .PaddingVertical(2.5f).PaddingHorizontal(3)
            .DefaultTextStyle(text => text.FontSize(ReportStyles.SmallFontSize).SemiBold());

    private static IContainer BodyCell(IContainer container) =>
        container
            .Border(0.6f).BorderColor(ReportStyles.LineColor)
            .MinHeight(17)
            .PaddingVertical(2).PaddingHorizontal(3)
            .AlignMiddle();

    private static void CheckCell(IContainer container, bool isChecked)
    {
        container.Width(9).Height(9).Border(0.9f).BorderColor(Colors.Black)
            .AlignCenter().AlignMiddle()
            .Text(isChecked ? "X" : string.Empty).FontSize(7.5f).Bold();
    }
}
