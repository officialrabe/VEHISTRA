using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace Vehistra.Infrastructure.ImportExport;

/// <summary>Liest CSV- und XLSX-Dateien einheitlich als Kopfzeile plus Datenzeilen.</summary>
public static class TabularFileReader
{
    public static bool IsSupported(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() is ".csv" or ".txt" or ".xlsx" or ".xlsm";

    public static (IReadOnlyList<string> Columns, List<string[]> Rows) Read(string filePath, int maxRows = int.MaxValue)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".csv" or ".txt" => ReadCsv(filePath, maxRows),
            ".xlsx" or ".xlsm" => ReadExcel(filePath, maxRows),
            _ => throw new NotSupportedException(
                $"Dateien vom Typ '{extension}' koennen nicht importiert werden. Bitte CSV oder XLSX verwenden.")
        };
    }

    private static (IReadOnlyList<string>, List<string[]>) ReadCsv(string filePath, int maxRows)
    {
        var delimiter = DetectDelimiter(filePath);

        var configuration = new CsvConfiguration(CultureInfo.GetCultureInfo("de-DE"))
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            DetectColumnCountChanges = false
        };

        using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, configuration);

        csv.Read();
        csv.ReadHeader();

        var columns = csv.HeaderRecord?.Select(h => h.Trim()).ToList() ?? [];
        var rows = new List<string[]>();

        while (csv.Read() && rows.Count < maxRows)
        {
            var row = new string[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                row[i] = csv.TryGetField<string>(i, out var value) ? value?.Trim() ?? string.Empty : string.Empty;
            }

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(row);
        }

        return (columns, rows);
    }

    private static (IReadOnlyList<string>, List<string[]>) ReadExcel(string filePath, int maxRows)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var range = worksheet.RangeUsed();

        if (range is null)
        {
            return ([], []);
        }

        var columns = range.FirstRow().Cells()
            .Select(c => c.GetString().Trim())
            .ToList();

        var rows = new List<string[]>();

        foreach (var excelRow in range.Rows().Skip(1))
        {
            if (rows.Count >= maxRows)
            {
                break;
            }

            var row = new string[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                row[i] = excelRow.Cell(i + 1).GetString().Trim();
            }

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(row);
        }

        return (columns, rows);
    }

    /// <summary>Erkennt das Trennzeichen anhand der Kopfzeile (Semikolon, Komma oder Tabulator).</summary>
    private static string DetectDelimiter(string filePath)
    {
        using var reader = new StreamReader(filePath);
        var firstLine = reader.ReadLine() ?? string.Empty;

        var candidates = new[] { ";", ",", "\t", "|" };

        return candidates
            .OrderByDescending(c => firstLine.Count(ch => ch.ToString() == c))
            .First();
    }

    /// <summary>Zaehlt die Datenzeilen einer Datei, ohne alles in den Speicher zu laden.</summary>
    public static int CountRows(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension is ".xlsx" or ".xlsm")
        {
            using var workbook = new XLWorkbook(filePath);
            var range = workbook.Worksheets.First().RangeUsed();
            return range is null ? 0 : Math.Max(0, range.RowCount() - 1);
        }

        var lines = 0;
        using var reader = new StreamReader(filePath);
        while (reader.ReadLine() is not null)
        {
            lines++;
        }

        return Math.Max(0, lines - 1);
    }
}
