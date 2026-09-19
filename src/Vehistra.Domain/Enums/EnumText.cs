using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Vehistra.Domain.Enums;

/// <summary>
/// Beschriftung eines Aufzaehlungswerts fuer die Anzeige - in der Oberflaeche,
/// in Ausdrucken und in Exporten gleichermassen.
///
/// Steht hier und nicht in der Oberflaeche, weil Listen auch nach Excel, CSV
/// und PDF gehen. Vorher stand dort der blanke Bezeichner: "WartetAufTeile"
/// in der Tabelle, "Wartet auf Teile" am Bildschirm.
/// </summary>
public static class EnumText
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Zuerst die am Wert hinterlegte Beschriftung, sonst der getrennte
    /// Bezeichner. Die Beschriftung ist noetig, weil sich richtiges Deutsch
    /// nicht aus dem Bezeichner ableiten laesst: "Wartet auf Teile" braucht
    /// das grosse T, "Nicht fahrbereit" das kleine f.
    /// </summary>
    public static string Of(object? wert)
    {
        if (wert is null)
        {
            return string.Empty;
        }

        var name = wert.ToString() ?? string.Empty;
        var typ = wert.GetType();

        if (typ.IsEnum && typ.GetField(name) is { } feld)
        {
            var angabe = feld.GetCustomAttribute<DescriptionAttribute>();

            if (angabe is not null && !string.IsNullOrWhiteSpace(angabe.Description))
            {
                return angabe.Description;
            }
        }

        return Trenne(name);
    }

    /// <summary>Trennt zusammengeschriebene Bezeichner ("ReparaturGeplant" -> "Reparatur geplant").</summary>
    public static string Trenne(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var text = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                text.Append(' ');
                text.Append(char.ToLower(name[i], Deutsch));
                continue;
            }

            text.Append(name[i]);
        }

        return text.ToString();
    }
}
