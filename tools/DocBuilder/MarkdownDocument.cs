using System.Text;
using System.Text.RegularExpressions;

namespace Vehistra.DocBuilder;

/// <summary>Art eines Blocks im Markdown-Dokument.</summary>
public enum BlockKind
{
    Heading1,
    Heading2,
    Heading3,
    Paragraph,
    Bullet,
    Numbered,
    Code,
    Quote,
    Rule,
    Table
}

/// <summary>Ein Abschnitt des Dokuments.</summary>
public sealed record DocumentBlock(BlockKind Kind, string Text)
{
    /// <summary>Zeilen einer Tabelle; die erste Zeile ist die Kopfzeile.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}

/// <summary>
/// Sehr schlanker Markdown-Leser. Er deckt genau die Auszeichnungen ab, die in den
/// Anleitungen vorkommen: Ueberschriften, Absaetze, Aufzaehlungen, nummerierte
/// Listen, Zitate, Trennlinien, Codebloecke und einfache Tabellen.
/// </summary>
public static partial class MarkdownDocument
{
    [GeneratedRegex(@"^(#{1,3})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\s*[-*]\s+(.*)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^\s*(\d+)\.\s+(.*)$")]
    private static partial Regex NumberedPattern();

    public static IReadOnlyList<DocumentBlock> Parse(string markdown)
    {
        var blocks = new List<DocumentBlock>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new StringBuilder();

        void FlushParagraph()
        {
            if (paragraph.Length == 0)
            {
                return;
            }

            blocks.Add(new DocumentBlock(BlockKind.Paragraph, Inline(paragraph.ToString().Trim())));
            paragraph.Clear();
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();

                var code = new StringBuilder();
                index++;

                while (index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    code.AppendLine(lines[index]);
                    index++;
                }

                blocks.Add(new DocumentBlock(BlockKind.Code, code.ToString().TrimEnd()));
                continue;
            }

            if (trimmed is "---" or "***" or "___")
            {
                FlushParagraph();
                blocks.Add(new DocumentBlock(BlockKind.Rule, string.Empty));
                continue;
            }

            var heading = HeadingPattern().Match(trimmed);
            if (heading.Success)
            {
                FlushParagraph();

                var kind = heading.Groups[1].Value.Length switch
                {
                    1 => BlockKind.Heading1,
                    2 => BlockKind.Heading2,
                    _ => BlockKind.Heading3
                };

                blocks.Add(new DocumentBlock(kind, Inline(heading.Groups[2].Value.Trim())));
                continue;
            }

            if (trimmed.StartsWith('|') && index + 1 < lines.Length && IsTableSeparator(lines[index + 1]))
            {
                FlushParagraph();

                var rows = new List<IReadOnlyList<string>> { SplitRow(trimmed) };
                index += 2;

                while (index < lines.Length && lines[index].TrimStart().StartsWith('|'))
                {
                    rows.Add(SplitRow(lines[index].Trim()));
                    index++;
                }

                index--;
                blocks.Add(new DocumentBlock(BlockKind.Table, string.Empty) { Rows = rows });
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                FlushParagraph();

                // Aufeinanderfolgende Zitatzeilen gehoeren zu einem Block.
                var quote = new StringBuilder(trimmed.TrimStart('>').Trim());

                while (index + 1 < lines.Length && lines[index + 1].TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    index++;
                    var part = lines[index].TrimStart().TrimStart('>').Trim();

                    if (part.Length == 0)
                    {
                        quote.AppendLine().AppendLine();
                    }
                    else
                    {
                        if (quote.Length > 0 && quote[^1] != '\n')
                        {
                            quote.Append(' ');
                        }

                        quote.Append(part);
                    }
                }

                blocks.Add(new DocumentBlock(BlockKind.Quote, Inline(quote.ToString().Trim())));
                continue;
            }

            var bullet = BulletPattern().Match(line);
            if (bullet.Success)
            {
                FlushParagraph();
                var text = bullet.Groups[1].Value.Trim() + ReadContinuation(lines, ref index);
                blocks.Add(new DocumentBlock(BlockKind.Bullet, Inline(text)));
                continue;
            }

            var numbered = NumberedPattern().Match(line);
            if (numbered.Success)
            {
                FlushParagraph();
                var text = numbered.Groups[2].Value.Trim() + ReadContinuation(lines, ref index);
                blocks.Add(new DocumentBlock(BlockKind.Numbered,
                    $"{numbered.Groups[1].Value}. {Inline(text)}"));
                continue;
            }

            paragraph.Append(trimmed).Append(' ');
        }

        FlushParagraph();
        return blocks;
    }

    /// <summary>
    /// Liest die Folgezeilen eines Listeneintrags. In Markdown darf ein Eintrag ueber
    /// mehrere Quellzeilen umbrochen sein; im PDF gehoert das in einen Absatz.
    /// </summary>
    private static string ReadContinuation(string[] lines, ref int index)
    {
        var continuation = new StringBuilder();

        while (index + 1 < lines.Length)
        {
            var next = lines[index + 1];
            var trimmed = next.Trim();

            if (trimmed.Length == 0
                || BulletPattern().IsMatch(next)
                || NumberedPattern().IsMatch(next)
                || HeadingPattern().IsMatch(trimmed)
                || trimmed.StartsWith(">", StringComparison.Ordinal)
                || trimmed.StartsWith("|", StringComparison.Ordinal)
                || trimmed.StartsWith("```", StringComparison.Ordinal)
                || trimmed is "---" or "***" or "___")
            {
                break;
            }

            index++;
            continuation.Append(' ').Append(trimmed);
        }

        return continuation.ToString();
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('|') && trimmed.Contains("---", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> SplitRow(string line) =>
        line.Trim('|')
            .Split('|')
            .Select(cell => Inline(cell.Trim()))
            .ToList();

    /// <summary>Entfernt die Inline-Auszeichnungen, die im PDF nicht gebraucht werden.</summary>
    private static string Inline(string text) =>
        text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
}
