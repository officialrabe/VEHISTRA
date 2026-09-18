using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Vehistra.UnitTests;

/// <summary>
/// Liest den sichtbaren Text aus einem PDF. Wird ausschliesslich von den Tests
/// verwendet, um den Inhalt der erzeugten Berichte zu pruefen.
///
/// Der Leser deckt genau das ab, was die Berichtserzeugung ausgibt: klassische
/// Objektstruktur, mit Flate komprimierte Stroeme und Text mit zwei Byte breiten
/// Glyphennummern, die ueber eine ToUnicode-Tabelle aufgeloest werden.
/// </summary>
public static partial class PdfTextReader
{
    [GeneratedRegex(@"(\d+)\s+(\d+)\s+obj\b")]
    private static partial Regex ObjectPattern();

    [GeneratedRegex(@"/Font\s*<<(?<body>[^>]*)>>", RegexOptions.Singleline)]
    private static partial Regex FontResourcePattern();

    [GeneratedRegex(@"/(?<name>[A-Za-z0-9]+)\s+(?<id>\d+)\s+\d+\s+R")]
    private static partial Regex ResourceEntryPattern();

    [GeneratedRegex(@"/ToUnicode\s+(\d+)\s+\d+\s+R")]
    private static partial Regex ToUnicodePattern();

    [GeneratedRegex(@"beginbfchar(?<body>.*?)endbfchar", RegexOptions.Singleline)]
    private static partial Regex BfCharPattern();

    [GeneratedRegex(@"beginbfrange(?<body>.*?)endbfrange", RegexOptions.Singleline)]
    private static partial Regex BfRangePattern();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")]
    private static partial Regex HexPairPattern();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(?:<([0-9A-Fa-f]+)>|\[(?<list>[^\]]*)\])")]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"/(?<name>[A-Za-z0-9]+)\s+[\d.]+\s+Tf")]
    private static partial Regex SelectFontPattern();

    [GeneratedRegex(@"<(?<hex>[0-9A-Fa-f]+)>")]
    private static partial Regex HexStringPattern();

    /// <summary>Liefert den gesamten sichtbaren Text des Dokuments.</summary>
    public static string Extract(byte[] pdf)
    {
        var objects = ReadObjects(pdf);
        var cmapsByObject = ReadCharacterMaps(objects);
        var cmapsByName = MapResourceNames(objects, cmapsByObject);

        var text = new StringBuilder();

        foreach (var body in objects.Values)
        {
            var content = Inflate(body);

            if (content is null || (!content.Contains("Tj", StringComparison.Ordinal)
                                    && !content.Contains("TJ", StringComparison.Ordinal)))
            {
                continue;
            }

            AppendContent(content, cmapsByName, text);
        }

        return text.ToString();
    }

    // ------------------------------------------------------------------ Objekte

    private sealed record PdfObject(string Dictionary, byte[]? Stream);

    private static Dictionary<int, PdfObject> ReadObjects(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var objects = new Dictionary<int, PdfObject>();

        foreach (Match match in ObjectPattern().Matches(raw))
        {
            var id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var start = match.Index + match.Length;
            var end = raw.IndexOf("endobj", start, StringComparison.Ordinal);

            if (end < 0)
            {
                continue;
            }

            var body = raw[start..end];
            var streamStart = body.IndexOf("stream", StringComparison.Ordinal);

            byte[]? stream = null;
            var dictionary = body;

            if (streamStart >= 0)
            {
                dictionary = body[..streamStart];

                var offset = start + streamStart + "stream".Length;
                if (offset < raw.Length && raw[offset] == '\r') { offset++; }
                if (offset < raw.Length && raw[offset] == '\n') { offset++; }

                var streamEnd = raw.IndexOf("endstream", offset, StringComparison.Ordinal);

                if (streamEnd > offset)
                {
                    stream = pdf[offset..streamEnd];
                }
            }

            objects[id] = new PdfObject(dictionary, stream);
        }

        return objects;
    }

    private static string? Inflate(PdfObject pdfObject)
    {
        if (pdfObject.Stream is null)
        {
            return null;
        }

        if (!pdfObject.Dictionary.Contains("FlateDecode", StringComparison.Ordinal))
        {
            return Encoding.Latin1.GetString(pdfObject.Stream);
        }

        try
        {
            using var source = new MemoryStream(pdfObject.Stream);
            using var decompressor = new ZLibStream(source, CompressionMode.Decompress);
            using var target = new MemoryStream();

            decompressor.CopyTo(target);
            return Encoding.Latin1.GetString(target.ToArray());
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------ ToUnicode

    private static Dictionary<int, Dictionary<int, string>> ReadCharacterMaps(
        Dictionary<int, PdfObject> objects)
    {
        var result = new Dictionary<int, Dictionary<int, string>>();

        foreach (var (id, body) in objects)
        {
            var reference = ToUnicodePattern().Match(body.Dictionary);

            if (!reference.Success)
            {
                continue;
            }

            var target = int.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture);

            if (!objects.TryGetValue(target, out var cmapObject))
            {
                continue;
            }

            var cmap = Inflate(cmapObject);

            if (cmap is not null)
            {
                result[id] = ParseCharacterMap(cmap);
            }
        }

        return result;
    }

    private static Dictionary<int, string> ParseCharacterMap(string cmap)
    {
        var map = new Dictionary<int, string>();

        foreach (Match section in BfCharPattern().Matches(cmap))
        {
            foreach (Match pair in HexPairPattern().Matches(section.Groups["body"].Value))
            {
                var code = System.Convert.ToInt32(pair.Groups[1].Value, 16);
                map[code] = DecodeUtf16(pair.Groups[2].Value);
            }
        }

        foreach (Match section in BfRangePattern().Matches(cmap))
        {
            foreach (Match range in RangePattern().Matches(section.Groups["body"].Value))
            {
                var low = System.Convert.ToInt32(range.Groups[1].Value, 16);
                var high = System.Convert.ToInt32(range.Groups[2].Value, 16);

                if (range.Groups[3].Success)
                {
                    var start = System.Convert.ToInt32(range.Groups[3].Value, 16);

                    for (var code = low; code <= high && code - low < 512; code++)
                    {
                        map[code] = char.ConvertFromUtf32(start + (code - low));
                    }

                    continue;
                }

                var index = 0;

                foreach (Match entry in HexStringPattern().Matches(range.Groups["list"].Value))
                {
                    map[low + index] = DecodeUtf16(entry.Groups["hex"].Value);
                    index++;
                }
            }
        }

        return map;
    }

    private static string DecodeUtf16(string hex)
    {
        var builder = new StringBuilder();

        for (var i = 0; i + 3 < hex.Length; i += 4)
        {
            builder.Append((char)System.Convert.ToInt32(hex.Substring(i, 4), 16));
        }

        return builder.ToString();
    }

    // ------------------------------------------------------- Ressourcennamen

    private static Dictionary<string, Dictionary<int, string>> MapResourceNames(
        Dictionary<int, PdfObject> objects,
        Dictionary<int, Dictionary<int, string>> cmapsByObject)
    {
        var result = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);

        foreach (var body in objects.Values)
        {
            foreach (Match resource in FontResourcePattern().Matches(body.Dictionary))
            {
                foreach (Match entry in ResourceEntryPattern().Matches(resource.Groups["body"].Value))
                {
                    var name = entry.Groups["name"].Value;
                    var id = int.Parse(entry.Groups["id"].Value, CultureInfo.InvariantCulture);

                    if (!cmapsByObject.TryGetValue(id, out var cmap))
                    {
                        continue;
                    }

                    if (!result.TryGetValue(name, out var existing))
                    {
                        result[name] = new Dictionary<int, string>(cmap);
                        continue;
                    }

                    // Derselbe Name in mehreren Seiten: Zuordnungen zusammenfuehren.
                    foreach (var (code, value) in cmap)
                    {
                        existing.TryAdd(code, value);
                    }
                }
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- Inhalt

    private static void AppendContent(
        string content,
        Dictionary<string, Dictionary<int, string>> cmapsByName,
        StringBuilder text)
    {
        Dictionary<int, string>? current = null;
        var position = 0;

        while (position < content.Length)
        {
            var font = SelectFontPattern().Match(content, position);
            var show = content.IndexOf('<', position);

            if (font.Success && (show < 0 || font.Index < show))
            {
                cmapsByName.TryGetValue(font.Groups["name"].Value, out current);
                position = font.Index + font.Length;
                continue;
            }

            if (show < 0)
            {
                break;
            }

            var close = content.IndexOf('>', show);

            if (close < 0)
            {
                break;
            }

            var hex = content[(show + 1)..close];
            position = close + 1;

            if (hex.Length == 0 || hex.Length % 4 != 0 || !IsHex(hex))
            {
                continue;
            }

            for (var i = 0; i + 3 < hex.Length; i += 4)
            {
                var code = System.Convert.ToInt32(hex.Substring(i, 4), 16);

                if (current is not null && current.TryGetValue(code, out var value))
                {
                    text.Append(value);
                }
            }
        }

        text.AppendLine();
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
