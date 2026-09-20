using Vehistra.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vehistra.Reporting.Documents;

/// <summary>Wiederverwendbare Bausteine der Formulare (Kopf, Abschnitte, Felder, Kaestchen).</summary>
internal static class ReportComponents
{
    /// <summary>Kopfbereich mit Unternehmensname, Logo und Berichtstitel.</summary>
    public static void Header(IContainer container, ReportHeaderData header, string title, string? subtitle, string? pageInfo)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(header.CompanyName)
                        .FontSize(13).Bold();

                    if (!string.IsNullOrWhiteSpace(header.CompanyAddress))
                    {
                        left.Item().Text(header.CompanyAddress)
                            .Style(ReportStyles.Small(TextStyle.Default));
                    }

                    left.Item().PaddingTop(2).Text(header.ApplicationTitle)
                        .FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2).LetterSpacing(0.12f);
                });

                if (!string.IsNullOrWhiteSpace(header.LogoPath) && File.Exists(header.LogoPath))
                {
                    row.ConstantItem(90).AlignRight().AlignTop().MaxHeight(38).Image(header.LogoPath).FitArea();
                }
            });

            column.Item().PaddingTop(6).BorderBottom(1.4f).BorderColor(Colors.Black).PaddingBottom(4).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(title).FontSize(ReportStyles.TitleFontSize).Bold().LetterSpacing(0.04f);

                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        left.Item().Text(subtitle).Style(ReportStyles.Small(TextStyle.Default));
                    }
                });

                if (!string.IsNullOrWhiteSpace(pageInfo))
                {
                    row.ConstantItem(90).AlignRight().AlignBottom()
                        .Text(pageInfo).Style(ReportStyles.Small(TextStyle.Default));
                }
            });
        });
    }

    /// <summary>
    /// Fusszeile mit Erstellungsangaben - ohne URL, ohne localhost.
    ///
    /// Beim Vordruck steht bewusst "Vordruck erstellt von": das Formular ist
    /// leer, ausgefuellt hat es niemand. "Erstellt von" wuerde dort so klingen,
    /// als stammten die spaeter eingetragenen Angaben von dieser Person.
    /// </summary>
    public static void Footer(IContainer container, ReportHeaderData header, bool istVordruck = false)
    {
        container.BorderTop(0.8f).BorderColor(Colors.Grey.Darken1).PaddingTop(3).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.DefaultTextStyle(ReportStyles.Small(TextStyle.Default));
                text.Span($"{header.CompanyName} · Vehistra {header.ApplicationVersion}");

                if (!string.IsNullOrWhiteSpace(header.PrintedBy))
                {
                    text.Span(istVordruck
                        ? $" · Vordruck erstellt von {header.PrintedBy}"
                        : $" · erstellt von {header.PrintedBy}");
                }

                text.Span($" · {header.PrintedAt:dd.MM.yyyy HH:mm}");
            });

            row.ConstantItem(120).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(ReportStyles.Small(TextStyle.Default));
                text.Span("Seite ");
                text.CurrentPageNumber();
                text.Span(" von ");
                text.TotalPages();
            });
        });
    }

    /// <summary>Abschnittsueberschrift mit grauem Balken.</summary>
    public static void SectionTitle(IContainer container, string title)
    {
        container
            .Background(ReportStyles.SectionBackground)
            .Border(0.8f).BorderColor(ReportStyles.LineColor)
            .PaddingVertical(2.5f).PaddingHorizontal(4)
            .Text(title.ToUpperInvariant()).Style(ReportStyles.Section(TextStyle.Default)).LetterSpacing(0.06f);
    }

    /// <summary>Beschriftetes Feld mit Unterstrich - fuer ausgefuellte und leere Formulare gleich.</summary>
    public static void Field(IContainer container, string label, string? value, bool underline = true)
    {
        container.Column(column =>
        {
            column.Item().Text(label.ToUpperInvariant()).Style(ReportStyles.Label(TextStyle.Default));

            var line = column.Item().MinHeight(ReportStyles.FieldHeight).PaddingTop(1);

            if (underline)
            {
                line = line.BorderBottom(0.7f).BorderColor(ReportStyles.LineColor);
            }

            line.Text(value ?? string.Empty).Style(ReportStyles.Value(TextStyle.Default));
        });
    }

    /// <summary>Ankreuzkaestchen mit Beschriftung.</summary>
    public static void CheckBox(IContainer container, string label, bool isChecked)
    {
        container.Row(row =>
        {
            row.ConstantItem(11).AlignMiddle().Height(9).Width(9)
                .Border(0.9f).BorderColor(Colors.Black)
                .AlignCenter().AlignMiddle()
                .Text(isChecked ? "X" : string.Empty)
                .FontSize(7.5f).Bold();

            row.RelativeItem().PaddingLeft(3).AlignMiddle()
                .Text(label).FontSize(ReportStyles.BaseFontSize - 0.5f);
        });
    }

    /// <summary>Ja-/Nein-Auswahl als zwei Kaestchen.</summary>
    public static void YesNo(IContainer container, string label, bool? value)
    {
        container.Column(column =>
        {
            column.Item().Text(label.ToUpperInvariant()).Style(ReportStyles.Label(TextStyle.Default));

            column.Item().PaddingTop(2).Row(row =>
            {
                row.ConstantItem(52).Element(c => CheckBox(c, "Ja", value == true));
                row.ConstantItem(52).Element(c => CheckBox(c, "Nein", value == false));
                row.RelativeItem();
            });
        });
    }

    /// <summary>Leere Schreibzeilen fuer handschriftliche Eintraege.</summary>
    public static void WritingLines(IContainer container, int lineCount, string? text = null, float lineHeight = 15f)
    {
        container.Column(column =>
        {
            var lines = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n');

            for (var i = 0; i < lineCount; i++)
            {
                var content = i < lines.Length ? lines[i] : string.Empty;

                column.Item()
                    .MinHeight(lineHeight)
                    .BorderBottom(0.6f).BorderColor(ReportStyles.LineColor)
                    .PaddingBottom(1).AlignBottom()
                    .Text(content).Style(ReportStyles.Value(TextStyle.Default));
            }
        });
    }

    /// <summary>Unterschriftsfeld mit Datumsangabe.</summary>
    public static void SignatureField(IContainer container, string caption)
    {
        container.Column(column =>
        {
            column.Item().Height(26);
            column.Item().BorderTop(0.8f).BorderColor(Colors.Black).PaddingTop(2)
                .Text(caption).Style(ReportStyles.Small(TextStyle.Default));
        });
    }
}
