using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Client.Converters;

/// <summary>Wandelt einen Wahrheitswert in Sichtbarkeit (true = sichtbar).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;

        if (Invert || string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility == Visibility.Visible;
}

/// <summary>Sichtbarkeit anhand eines gefuellten Textes.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Sichtbarkeit anhand eines vorhandenen Objekts.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null;

        if (Invert || string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt eine Warnstufe in die zugehoerige Vordergrundfarbe.</summary>
public sealed class WarningLevelToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value as WarningLevel? ?? WarningLevel.Inaktiv;
        var useBackground = Background || string.Equals(parameter as string, "background", StringComparison.OrdinalIgnoreCase);

        var key = level switch
        {
            WarningLevel.Ok => useBackground ? "OkBackgroundBrush" : "OkBrush",
            WarningLevel.Hinweis => useBackground ? "InfoBackgroundBrush" : "InfoBrush",
            WarningLevel.BaldFaellig => useBackground ? "WarnBackgroundBrush" : "WarnBrush",
            WarningLevel.Kritisch => useBackground ? "CriticalBackgroundBrush" : "CriticalBrush",
            _ => useBackground ? "InactiveBackgroundBrush" : "InactiveBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Kurzer Klartext zu einer Warnstufe - Farbe ist nie die einzige Information.</summary>
public sealed class WarningLevelToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as WarningLevel?) switch
        {
            WarningLevel.Ok => "OK",
            WarningLevel.Hinweis => "Hinweis",
            WarningLevel.BaldFaellig => "Bald fällig",
            WarningLevel.Kritisch => "Kritisch",
            _ => "–"
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt einen Farbcode (#RRGGBB) aus den Stammdaten in einen Pinsel.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            }
            catch (FormatException)
            {
                // Faellt auf die Standardfarbe zurueck.
            }
        }

        return System.Windows.Application.Current?.TryFindResource("InactiveBrush") as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt einen Aufzaehlungswert in eine lesbare Bezeichnung.</summary>
public sealed class EnumToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? string.Empty : Humanize(value.ToString()!);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>Trennt zusammengeschriebene Bezeichner ("WartetAufTeile" -> "Wartet auf Teile").</summary>
    public static string Humanize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                builder.Append(' ');
                builder.Append(char.ToLower(name[i], culture: CultureInfo.GetCultureInfo("de-DE")));
                continue;
            }

            builder.Append(name[i]);
        }

        return builder.ToString();
    }
}

/// <summary>Formatiert Dateigroessen.</summary>
public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bytes = value switch
        {
            long l => l,
            int i => i,
            _ => 0L
        };

        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:N1} MB",
            _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:N2} GB"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Zeigt die Anzahl ungelesener Benachrichtigungen nur bei Werten groesser null.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt die Dringlichkeit einer Benachrichtigung in eine Farbe.</summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as NotificationSeverity?) switch
        {
            NotificationSeverity.Kritisch => Background ? "CriticalBackgroundBrush" : "CriticalBrush",
            NotificationSeverity.Warnung => Background ? "WarnBackgroundBrush" : "WarnBrush",
            _ => Background ? "SurfaceSunkenBrush" : "TextMutedBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt einen Schadensstatus in die passende Farbe.</summary>
public sealed class DamageStatusToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as DamageStatus?) switch
        {
            DamageStatus.Geschlossen => Background ? "OkBackgroundBrush" : "OkBrush",
            DamageStatus.Repariert => Background ? "OkBackgroundBrush" : "OkBrush",
            DamageStatus.Werkstatt => Background ? "InfoBackgroundBrush" : "InfoBrush",
            DamageStatus.ReparaturGeplant => Background ? "InfoBackgroundBrush" : "InfoBrush",
            DamageStatus.Gemeldet => Background ? "WarnBackgroundBrush" : "WarnBrush",
            DamageStatus.Geprueft => Background ? "WarnBackgroundBrush" : "WarnBrush",
            _ => Background ? "InactiveBackgroundBrush" : "InactiveBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt eine Schadenspriorität in die passende Farbe.</summary>
public sealed class DamagePriorityToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as DamagePriority?) switch
        {
            DamagePriority.Kritisch => Background ? "CriticalBackgroundBrush" : "CriticalBrush",
            DamagePriority.Hoch => Background ? "WarnBackgroundBrush" : "WarnBrush",
            DamagePriority.Normal => Background ? "InfoBackgroundBrush" : "InfoBrush",
            _ => Background ? "InactiveBackgroundBrush" : "InactiveBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt einen Werkstattstatus in die passende Farbe.</summary>
public sealed class WorkshopStatusToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as WorkshopOrderStatus?) switch
        {
            WorkshopOrderStatus.Abgeholt => Background ? "OkBackgroundBrush" : "OkBrush",
            WorkshopOrderStatus.Fertig => Background ? "OkBackgroundBrush" : "OkBrush",
            WorkshopOrderStatus.WartetAufTeile => Background ? "WarnBackgroundBrush" : "WarnBrush",
            WorkshopOrderStatus.InBearbeitung => Background ? "InfoBackgroundBrush" : "InfoBrush",
            WorkshopOrderStatus.FahrzeugAbgegeben => Background ? "InfoBackgroundBrush" : "InfoBrush",
            WorkshopOrderStatus.Storniert => Background ? "InactiveBackgroundBrush" : "InactiveBrush",
            _ => Background ? "SurfaceSunkenBrush" : "TextMutedBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wandelt einen Kennzeichenstatus in die passende Farbe.</summary>
public sealed class PlateStatusToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as LicensePlateStatus?) switch
        {
            LicensePlateStatus.Verfuegbar => Background ? "OkBackgroundBrush" : "OkBrush",
            LicensePlateStatus.Reserviert => Background ? "InfoBackgroundBrush" : "InfoBrush",
            LicensePlateStatus.Vergeben => Background ? "SurfaceSunkenBrush" : "TextMutedBrush",
            _ => Background ? "InactiveBackgroundBrush" : "InactiveBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Zeigt "Ja" oder "Nein" statt True/False.</summary>
public sealed class BoolToYesNoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            bool flag => flag ? "Ja" : "Nein",
            null => "–",
            _ => value.ToString() ?? string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value as string == "Ja";
}

/// <summary>Kehrt einen Wahrheitswert um (z. B. um Felder zu sperren).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool flag || !flag;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool flag || !flag;
}
