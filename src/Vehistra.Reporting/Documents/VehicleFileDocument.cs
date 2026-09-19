using Vehistra.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vehistra.Reporting.Documents;

/// <summary>Fahrzeugakte: Stammdaten sowie alle Vorgaenge eines Fahrzeugs als DIN-A4-Bericht.</summary>
internal sealed class VehicleFileDocument : IDocument
{
    private readonly VehicleFileReportData _data;

    public VehicleFileDocument(VehicleFileReportData data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Fahrzeugakte {_data.VehicleDisplay}",
        Author = _data.Header.CompanyName,
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
                .FontSize(8f)
                .FontColor(Colors.Black));

            page.Header().Element(header => ReportComponents.Header(
                header, _data.Header, "FAHRZEUGAKTE", _data.VehicleDisplay, null));

            page.Content().PaddingTop(6).Element(Content);
            page.Footer().Element(footer => ReportComponents.Footer(footer, _data.Header));
        });
    }

    private void Content(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item().Element(c => ReportComponents.SectionTitle(c, "Stammdaten"));

            column.Item().Border(0.6f).BorderColor(ReportStyles.LineColor).BorderTop(0).Padding(5).Column(master =>
            {
                var entries = _data.MasterData.ToList();
                var rows = (int)Math.Ceiling(entries.Count / 3.0);

                for (var rowIndex = 0; rowIndex < rows; rowIndex++)
                {
                    master.Item().PaddingVertical(1.5f).Row(row =>
                    {
                        row.Spacing(10);

                        for (var columnIndex = 0; columnIndex < 3; columnIndex++)
                        {
                            var index = rowIndex * 3 + columnIndex;

                            if (index >= entries.Count)
                            {
                                row.RelativeItem();
                                continue;
                            }

                            var entry = entries[index];

                            row.RelativeItem().Column(field =>
                            {
                                field.Item().Text(entry.Label.ToUpperInvariant())
                                    .Style(ReportStyles.Label(TextStyle.Default));
                                field.Item().Text(string.IsNullOrWhiteSpace(entry.Value) ? "–" : entry.Value)
                                    .FontSize(8.5f);
                            });
                        }
                    });
                }
            });

            foreach (var section in _data.Sections)
            {
                column.Item().Element(c => ReportComponents.SectionTitle(c, section.Title));

                if (section.Rows.Count == 0)
                {
                    column.Item().Border(0.6f).BorderColor(ReportStyles.LineColor).BorderTop(0).Padding(5)
                        .Text("Keine Einträge vorhanden.").Italic().FontSize(7.5f);
                    continue;
                }

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in section.Columns)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var columnName in section.Columns)
                        {
                            header.Cell()
                                .Border(0.6f).BorderColor(ReportStyles.LineColor)
                                .Background(Colors.Grey.Lighten3)
                                .PaddingVertical(2).PaddingHorizontal(3)
                                .Text(columnName).FontSize(7f).SemiBold();
                        }
                    });

                    foreach (var row in section.Rows)
                    {
                        for (var i = 0; i < section.Columns.Count; i++)
                        {
                            var value = i < row.Count ? row[i] : null;

                            table.Cell()
                                .Border(0.4f).BorderColor(Colors.Grey.Lighten1)
                                .PaddingVertical(2).PaddingHorizontal(3)
                                .Text(value ?? string.Empty).FontSize(7.5f);
                        }
                    }
                });
            }
        });
    }
}
