using Fuhrpark.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fuhrpark.Reporting.Documents;

/// <summary>Beliebige Liste als DIN-A4-Bericht (wahlweise Hoch- oder Querformat).</summary>
internal sealed class TableReportDocument : IDocument
{
    private readonly TableReportData _data;

    public TableReportDocument(TableReportData data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = _data.Title,
        Author = _data.Header.CompanyName,
        Creator = "Fuhrparkmanagement - LSP Virtual Services",
        Producer = "Fuhrparkmanagement"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(_data.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(12, Unit.Millimetre);
            page.DefaultTextStyle(text => text
                .FontFamily(ReportStyles.FontFamily)
                .FontSize(7.5f)
                .FontColor(Colors.Black));

            page.Header().Element(header => ReportComponents.Header(
                header, _data.Header, _data.Title.ToUpperInvariant(), _data.Subtitle, null));

            page.Content().PaddingTop(6).Element(Content);
            page.Footer().Element(footer => ReportComponents.Footer(footer, _data.Header));
        });
    }

    private void Content(IContainer container)
    {
        if (_data.Columns.Count == 0)
        {
            container.Text("Es liegen keine Daten vor.").Italic();
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in _data.Columns)
                {
                    columns.RelativeColumn();
                }
            });

            table.Header(header =>
            {
                foreach (var column in _data.Columns)
                {
                    header.Cell().Element(HeaderCell).Text(column);
                }
            });

            var rowIndex = 0;

            foreach (var row in _data.Rows)
            {
                var isEven = rowIndex++ % 2 == 0;

                for (var i = 0; i < _data.Columns.Count; i++)
                {
                    var value = i < row.Count ? row[i] : null;
                    table.Cell().Element(cell => BodyCell(cell, isEven)).Text(value ?? string.Empty);
                }
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Border(0.6f).BorderColor(ReportStyles.LineColor)
            .Background(ReportStyles.SectionBackground)
            .PaddingVertical(3).PaddingHorizontal(3)
            .DefaultTextStyle(text => text.SemiBold());

    private static IContainer BodyCell(IContainer container, bool isEven) =>
        container
            .Border(0.4f).BorderColor(Colors.Grey.Lighten1)
            .Background(isEven ? Colors.White : Colors.Grey.Lighten5)
            .PaddingVertical(2).PaddingHorizontal(3);
}
