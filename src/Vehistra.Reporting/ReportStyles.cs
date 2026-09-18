using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vehistra.Reporting;

/// <summary>
/// Gemeinsame Gestaltung aller Berichte: DIN A4, schwarz/weiss gut druckbar,
/// klare Linien und gut lesbare Typografie.
/// </summary>
internal static class ReportStyles
{
    /// <summary>In QuestPDF eingebettete Schriftfamilie - keine Abhaengigkeit von Systemschriften.</summary>
    public const string FontFamily = "Lato";

    public const float BaseFontSize = 9f;
    public const float SmallFontSize = 7.5f;
    public const float LabelFontSize = 7f;
    public const float SectionFontSize = 9.5f;
    public const float TitleFontSize = 16f;

    public static readonly string LineColor = Colors.Grey.Darken1;
    public static readonly string SectionBackground = Colors.Grey.Lighten2;
    public static readonly string FieldBackground = Colors.Grey.Lighten4;

    /// <summary>Hoehe eines Eingabefelds im Formular.</summary>
    public const float FieldHeight = 16f;

    public static TextStyle Label(TextStyle style) =>
        style.FontSize(LabelFontSize).FontColor(Colors.Grey.Darken3).SemiBold();

    public static TextStyle Value(TextStyle style) =>
        style.FontSize(BaseFontSize).FontColor(Colors.Black);

    public static TextStyle Section(TextStyle style) =>
        style.FontSize(SectionFontSize).Bold().FontColor(Colors.Black);

    public static TextStyle Small(TextStyle style) =>
        style.FontSize(SmallFontSize).FontColor(Colors.Grey.Darken2);
}
