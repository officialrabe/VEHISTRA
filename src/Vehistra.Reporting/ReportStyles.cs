using System.IO;
using System.Linq;
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
    /// <summary>Mitgelieferte Schriftfamilie der PDF-Bibliothek.</summary>
    public const string MitgelieferteSchrift = "Lato";

    /// <summary>Ausweichschrift, die auf jedem Windows vorhanden ist.</summary>
    public const string Ausweichschrift = "Segoe UI";

    /// <summary>
    /// Schriftfamilie der Berichte. Normalerweise die mitgelieferte; fehlt ihr
    /// Archiv neben dem Programm, wird auf eine Systemschrift ausgewichen.
    ///
    /// Der Grund ist eine echte Panne: der Installer kopierte die Datei
    /// QuestPDF.Fonts.Lato.br nicht mit, weil sie in kein Dateimuster passte.
    /// Auf einer installierten Vehistra schlug daraufhin JEDER Ausdruck fehl,
    /// waehrend im Bauprozess alles gruen war - dort liegt die Datei daneben.
    /// Der Installer nimmt sie jetzt mit; dieser Ausweg sorgt dafuer, dass ein
    /// solcher Fehler den Ausdruck hoechstens anders aussehen laesst, statt ihn
    /// unmoeglich zu machen.
    /// </summary>
    public static string FontFamily { get; } = SchriftarchivVorhanden()
        ? MitgelieferteSchrift
        : Ausweichschrift;

    /// <summary>Sucht das Schriftarchiv dort, wo die Bibliothek es erwartet.</summary>
    private static bool SchriftarchivVorhanden() =>
        new[] { "QuestPDF.Fonts.Lato.br", "QuestPDF.Fonts.Lato.gz" }
            .Any(datei => File.Exists(Path.Combine(AppContext.BaseDirectory, datei)));

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
