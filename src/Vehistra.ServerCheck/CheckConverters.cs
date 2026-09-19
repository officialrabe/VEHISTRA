using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Vehistra.ServerCheck;

/// <summary>Faerbt das Ergebnis-Badge. Die Farbe ergaenzt den Text, sie ersetzt ihn nie.</summary>
public sealed class CheckStateBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Ok = new(Color.FromRgb(0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(0xB5, 0x6A, 0x00));
    private static readonly SolidColorBrush Problem = new(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly SolidColorBrush Skipped = new(Color.FromRgb(0x5E, 0x6B, 0x79));

    private static readonly SolidColorBrush OkBackground = new(Color.FromRgb(0xE5, 0xF3, 0xE6));
    private static readonly SolidColorBrush WarningBackground = new(Color.FromRgb(0xFD, 0xF3, 0xE0));
    private static readonly SolidColorBrush ProblemBackground = new(Color.FromRgb(0xFD, 0xEC, 0xEC));
    private static readonly SolidColorBrush SkippedBackground = new(Color.FromRgb(0xF0, 0xF2, 0xF4));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var background = parameter is string text && text.Equals("background", StringComparison.OrdinalIgnoreCase);

        return value switch
        {
            CheckState.Ok => background ? OkBackground : Ok,
            CheckState.Warning => background ? WarningBackground : Warning,
            CheckState.Problem => background ? ProblemBackground : Problem,
            _ => background ? SkippedBackground : Skipped
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Wandelt einen Wahrheitswert in Sichtbarkeit um.</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;

        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Blendet einen Bereich nur bei vorhandenem Text ein.</summary>
public sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
