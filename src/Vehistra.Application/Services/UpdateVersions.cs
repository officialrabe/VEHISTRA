namespace Vehistra.Application.Services;

/// <summary>
/// Vergleich zweier Versionsangaben nach HAUPT.NEBEN.KORREKTUR. Steht hier und
/// nicht in einem der Dienste, weil Updateablage und Veroeffentlichungsseite
/// dieselbe Regel brauchen - zwei Fassungen waeren zwei Gelegenheiten, sich
/// unterschiedlich zu irren.
/// </summary>
public static class UpdateVersions
{
    /// <summary>
    /// Vergleicht zwei Versionsangaben. Rueckgabe <c>false</c>, wenn eine davon
    /// nicht lesbar ist; dann sagt <paramref name="comparison"/> nichts aus.
    /// </summary>
    public static bool TryCompare(string left, string right, out int comparison)
    {
        comparison = 0;

        if (!Version.TryParse(Normalisiere(left), out var links)
            || !Version.TryParse(Normalisiere(right), out var rechts))
        {
            return false;
        }

        comparison = links.CompareTo(rechts);
        return true;
    }

    /// <summary>"1.3" wird zu "1.3.0", Vorabkennungen wie "-beta" entfallen.</summary>
    private static string Normalisiere(string version)
    {
        var kern = (version ?? string.Empty).Split('-', '+')[0].Trim();
        var teile = kern.Split('.');

        return teile.Length switch
        {
            1 => $"{kern}.0.0",
            2 => $"{kern}.0",
            _ => kern
        };
    }
}
