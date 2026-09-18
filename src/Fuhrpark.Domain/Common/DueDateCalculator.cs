using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Domain.Common;

/// <summary>
/// Berechnet Warnstufen fuer Fristen. Die Schwellwerte sind administrativ konfigurierbar
/// und werden aus den Systemeinstellungen uebergeben.
/// </summary>
public static class DueDateCalculator
{
    /// <summary>Standardschwellen: kritisch ab abgelaufen, "bald faellig" ab 14 Tagen, Hinweis ab 30 Tagen.</summary>
    public static readonly DueDateThresholds DefaultThresholds = new(14, 30);

    public static WarningLevel Evaluate(DateTime? dueDate, DateTime today, DueDateThresholds thresholds)
    {
        if (dueDate is null)
        {
            return WarningLevel.Inaktiv;
        }

        var days = (dueDate.Value.Date - today.Date).Days;

        if (days < 0)
        {
            return WarningLevel.Kritisch;
        }

        if (days <= thresholds.CriticalDays)
        {
            return WarningLevel.BaldFaellig;
        }

        return days <= thresholds.WarningDays ? WarningLevel.Hinweis : WarningLevel.Ok;
    }

    /// <summary>Verbleibende Tage bis zur Faelligkeit (negativ = ueberfaellig).</summary>
    public static int? DaysUntil(DateTime? dueDate, DateTime today) =>
        dueDate is null ? null : (dueDate.Value.Date - today.Date).Days;

    /// <summary>Lesbarer Text fuer Listen und Berichte.</summary>
    public static string Describe(DateTime? dueDate, DateTime today)
    {
        if (dueDate is null)
        {
            return "Kein Termin hinterlegt";
        }

        var days = (dueDate.Value.Date - today.Date).Days;
        return days switch
        {
            < 0 => $"abgelaufen seit {Math.Abs(days)} Tag(en)",
            0 => "heute faellig",
            1 => "morgen faellig",
            _ => $"faellig in {days} Tagen"
        };
    }
}

/// <summary>Konfigurierbare Warnschwellen in Tagen.</summary>
public readonly record struct DueDateThresholds(int CriticalDays, int WarningDays);
