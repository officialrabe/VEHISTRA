using System.Text.RegularExpressions;

namespace Fuhrpark.Domain.Common;

/// <summary>
/// Normalisiert deutsche Kennzeichen, damit Suche und Eindeutigkeitspruefung unabhaengig
/// von der Schreibweise funktionieren ("fds ab123" -> "FDS-AB 123").
/// </summary>
public static partial class LicensePlateFormatter
{
    [GeneratedRegex(@"^([A-ZÄÖÜ]{1,3})[\s\-]*([A-ZÄÖÜ]{1,2})[\s\-]*(\d{1,4})\s*([EH]?)$", RegexOptions.CultureInvariant)]
    private static partial Regex PlatePattern();

    /// <summary>Normalisiert ein Kennzeichen. Nicht erkannte Eingaben werden nur getrimmt und gross geschrieben.</summary>
    public static string Normalize(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            return string.Empty;
        }

        var cleaned = plate.Trim().ToUpperInvariant();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        var match = PlatePattern().Match(cleaned.Replace(" ", string.Empty).Length <= 10 ? cleaned : cleaned);
        if (!match.Success)
        {
            return cleaned;
        }

        var suffix = match.Groups[4].Value;
        return $"{match.Groups[1].Value}-{match.Groups[2].Value} {match.Groups[3].Value}{suffix}";
    }

    /// <summary>Vergleichsschluessel ohne Trennzeichen fuer Suche und Duplikatspruefung.</summary>
    public static string ToComparisonKey(string? plate) =>
        string.IsNullOrWhiteSpace(plate)
            ? string.Empty
            : Regex.Replace(plate.ToUpperInvariant(), @"[^A-Z0-9ÄÖÜ]", string.Empty);

    /// <summary>Prueft, ob die Eingabe dem Muster eines deutschen Kennzeichens entspricht.</summary>
    public static bool IsPlausible(string? plate) =>
        !string.IsNullOrWhiteSpace(plate) && PlatePattern().IsMatch(plate.Trim().ToUpperInvariant());
}
