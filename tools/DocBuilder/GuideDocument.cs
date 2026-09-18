using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vehistra.DocBuilder;

/// <summary>
/// Erzeugt aus den geparsten Markdown-Bloecken ein echtes Vektor-PDF im DIN-A4-Format.
/// Es wird nichts aus einem Browser gedruckt und kein Bildschirmfoto eingebettet.
/// </summary>
public sealed class GuideDocument : IDocument
{
    private const string FontFamily = "Lato";

    // Wird als eingebettete Ressource mitgeliefert (siehe Program.cs).
    private const string CodeFontFamily = "DejaVu Sans Mono";

    private static readonly Color Accent = Color.FromHex("#0D6EFD");
    private static readonly Color Ink = Color.FromHex("#1C2430");
    private static readonly Color Muted = Color.FromHex("#5E6B79");
    private static readonly Color Line = Color.FromHex("#D3D9DE");
    private static readonly Color Panel = Color.FromHex("#F4F6F8");

    private readonly string _title;
    private readonly string _version;
    private readonly IReadOnlyList<DocumentBlock> _blocks;

    public GuideDocument(string title, string version, IReadOnlyList<DocumentBlock> blocks)
    {
        _title = title;
        _version = version;
        _blocks = blocks;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = _title,
        Author = "LSP Virtual Services",
        Subject = "Vehistra – Open Fleet Management",
        Keywords = "Vehistra, Fuhrpark, Anleitung, LSP Virtual Services",
        Creator = "Vehistra DocBuilder",
        Producer = "Vehistra DocBuilder"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(20, Unit.Millimetre);
            page.DefaultTextStyle(text => text.FontFamily(FontFamily).FontSize(10).FontColor(Ink));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(8).Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container
            .PaddingBottom(8)
            .BorderBottom(1)
            .BorderColor(Line)
            .Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("VEHISTRA").FontSize(13).SemiBold().FontColor(Accent);
                    column.Item().Text("Open Fleet Management").FontSize(8).FontColor(Muted);
                });

                row.ConstantItem(260).AlignRight().Column(column =>
                {
                    column.Item().AlignRight().Text(_title).FontSize(10).SemiBold();
                    column.Item().AlignRight().Text($"Version {_version}").FontSize(8).FontColor(Muted);
                });
            });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(0);

            foreach (var block in _blocks)
            {
                switch (block.Kind)
                {
                    case BlockKind.Heading1:
                        column.Item().PaddingTop(4).PaddingBottom(10).Column(inner =>
                        {
                            inner.Item().Text(block.Text).FontSize(19).SemiBold().FontColor(Accent);
                            inner.Item().PaddingTop(5).Height(2).Background(Accent).Width(60);
                        });
                        break;

                    case BlockKind.Heading2:
                        column.Item().PaddingTop(14).PaddingBottom(5)
                            .Text(block.Text).FontSize(13).SemiBold().FontColor(Ink);
                        break;

                    case BlockKind.Heading3:
                        column.Item().PaddingTop(9).PaddingBottom(3)
                            .Text(block.Text).FontSize(11).SemiBold().FontColor(Ink);
                        break;

                    case BlockKind.Paragraph:
                        column.Item().PaddingBottom(5).Text(block.Text).LineHeight(1.35f);
                        break;

                    case BlockKind.Bullet:
                        column.Item().PaddingBottom(3).PaddingLeft(6).Row(row =>
                        {
                            row.ConstantItem(12).Text("•").FontColor(Accent);
                            row.RelativeItem().Text(block.Text).LineHeight(1.3f);
                        });
                        break;

                    case BlockKind.Numbered:
                        var separator = block.Text.IndexOf(". ", StringComparison.Ordinal);
                        var number = separator < 0 ? string.Empty : block.Text[..(separator + 1)];
                        var body = separator < 0 ? block.Text : block.Text[(separator + 2)..];

                        column.Item().PaddingBottom(3).PaddingLeft(6).Row(row =>
                        {
                            row.ConstantItem(20).Text(number).SemiBold().FontColor(Accent);
                            row.RelativeItem().Text(body).LineHeight(1.3f);
                        });
                        break;

                    case BlockKind.Quote:
                        column.Item().PaddingVertical(5).Background(Panel)
                            .BorderLeft(3).BorderColor(Accent).Padding(9)
                            .Text(block.Text).LineHeight(1.3f);
                        break;

                    case BlockKind.Code:
                        column.Item().PaddingVertical(5).Background(Panel)
                            .Border(1).BorderColor(Line).Padding(9)
                            .Text(block.Text).FontFamily(CodeFontFamily).FontSize(9);
                        break;

                    case BlockKind.Rule:
                        column.Item().PaddingVertical(8).Height(1).Background(Line);
                        break;

                    case BlockKind.Table:
                        ComposeTable(column.Item().PaddingVertical(6), block);
                        break;
                }
            }
        });
    }

    private void ComposeTable(IContainer container, DocumentBlock block)
    {
        if (block.Rows.Count == 0)
        {
            return;
        }

        var columnCount = block.Rows.Max(r => r.Count);

        container.Border(1).BorderColor(Line).Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                for (var i = 0; i < columnCount; i++)
                {
                    definition.RelativeColumn();
                }
            });

            foreach (var cell in block.Rows[0])
            {
                table.Cell().Background(Panel).BorderBottom(1).BorderColor(Line).Padding(6)
                    .Text(cell).SemiBold().FontSize(9);
            }

            for (var missing = block.Rows[0].Count; missing < columnCount; missing++)
            {
                table.Cell().Background(Panel).BorderBottom(1).BorderColor(Line).Padding(6).Text(string.Empty);
            }

            foreach (var row in block.Rows.Skip(1))
            {
                foreach (var cell in row)
                {
                    table.Cell().BorderBottom(1).BorderColor(Line).Padding(6).Text(cell).FontSize(9);
                }

                for (var missing = row.Count; missing < columnCount; missing++)
                {
                    table.Cell().BorderBottom(1).BorderColor(Line).Padding(6).Text(string.Empty);
                }
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container
            .PaddingTop(7)
            .BorderTop(1)
            .BorderColor(Line)
            .Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                    text.Span("Vehistra · Developed & maintained by LSP Virtual Services · ");
                    text.Span("support@vehistra.dev");
                });

                row.ConstantItem(120).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                    text.Span("Seite ");
                    text.CurrentPageNumber();
                    text.Span(" von ");
                    text.TotalPages();
                });
            });
    }
}
