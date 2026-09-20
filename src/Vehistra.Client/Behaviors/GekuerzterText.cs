using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vehistra.Client.Behaviors;

/// <summary>
/// Zeigt den vollstaendigen Wert einer Tabellenzelle als Tooltip, aber nur
/// dann, wenn er nicht ganz in die Spalte passt. Ohne das endete ein langer
/// Modellname mitten im Wort, und es gab keinen Weg, den Rest zu sehen; ein
/// Tooltip an jeder Zelle waere dagegen nur laestig.
/// </summary>
public static class GekuerzterText
{
    public static readonly DependencyProperty ZeigeGanzenWertProperty =
        DependencyProperty.RegisterAttached(
            "ZeigeGanzenWert",
            typeof(bool),
            typeof(GekuerzterText),
            new PropertyMetadata(false, OnZeigeGanzenWertChanged));

    public static bool GetZeigeGanzenWert(DependencyObject element) =>
        (bool)element.GetValue(ZeigeGanzenWertProperty);

    public static void SetZeigeGanzenWert(DependencyObject element, bool value) =>
        element.SetValue(ZeigeGanzenWertProperty, value);

    private static void OnZeigeGanzenWertChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block)
        {
            return;
        }

        block.ToolTipOpening -= OnToolTipOpening;

        if (e.NewValue is true)
        {
            block.ToolTipOpening += OnToolTipOpening;
        }
    }

    private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (sender is not TextBlock block)
        {
            return;
        }

        // Handled = true unterdrueckt den Tooltip. Die Breite wird gerechnet,
        // nicht gemessen: ein Measure waehrend des Oeffnens wuerde das Layout
        // der Tabelle anfassen.
        if (!PasstNicht(block))
        {
            e.Handled = true;
        }
    }

    private static bool PasstNicht(TextBlock block)
    {
        if (string.IsNullOrEmpty(block.Text) || block.ActualWidth <= 0)
        {
            return false;
        }

        var text = new FormattedText(
            block.Text,
            CultureInfo.CurrentUICulture,
            block.FlowDirection,
            new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch),
            block.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(block).PixelsPerDip);

        // Ein halbes Pixel Spielraum gegen Rundungen.
        return text.Width > block.ActualWidth + 0.5;
    }
}
