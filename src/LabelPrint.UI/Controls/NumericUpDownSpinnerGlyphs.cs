using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace LabelPrint.UI.Controls;

/// <summary>
/// Replaces NumericUpDown spinner chevrons with + / −.
/// Fluent sets PathIcon as local Content inside ButtonSpinner — styles cannot override it.
/// </summary>
public static class NumericUpDownSpinnerGlyphs
{
    public static readonly AttachedProperty<bool> UsePlusMinusProperty =
        AvaloniaProperty.RegisterAttached<NumericUpDown, bool>(
            "UsePlusMinus",
            typeof(NumericUpDownSpinnerGlyphs));

    private static readonly AttachedProperty<bool> HookedProperty =
        AvaloniaProperty.RegisterAttached<NumericUpDown, bool>(
            "Hooked",
            typeof(NumericUpDownSpinnerGlyphs));

    static NumericUpDownSpinnerGlyphs()
    {
        UsePlusMinusProperty.Changed.AddClassHandler<NumericUpDown>(OnUsePlusMinusChanged);
    }

    public static bool GetUsePlusMinus(NumericUpDown element) =>
        element.GetValue(UsePlusMinusProperty);

    public static void SetUsePlusMinus(NumericUpDown element, bool value) =>
        element.SetValue(UsePlusMinusProperty, value);

    private static void OnUsePlusMinusChanged(NumericUpDown control, AvaloniaPropertyChangedEventArgs e)
    {
        if (!e.GetNewValue<bool>())
        {
            return;
        }

        if (control.GetValue(HookedProperty))
        {
            ScheduleApply(control);
            return;
        }

        control.SetValue(HookedProperty, true);
        control.TemplateApplied += (_, _) => ScheduleApply(control);
        control.AttachedToVisualTree += (_, _) => ScheduleApply(control);
        ScheduleApply(control);
    }

    private static void ScheduleApply(NumericUpDown control) =>
        Dispatcher.UIThread.Post(() => ApplyGlyphs(control), DispatcherPriority.Loaded);

    private static void ApplyGlyphs(NumericUpDown control)
    {
        if (!GetUsePlusMinus(control))
        {
            return;
        }

        control.TextAlignment = TextAlignment.Center;
        control.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;

        var textBox = FindNamed<TextBox>(control, "PART_TextBox");
        if (textBox is not null)
        {
            textBox.TextAlignment = TextAlignment.Center;
            textBox.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            textBox.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        var increase = FindNamed<RepeatButton>(control, "PART_IncreaseButton");
        var decrease = FindNamed<RepeatButton>(control, "PART_DecreaseButton");
        if (increase is null || decrease is null)
        {
            // Spinner template may apply one tick later.
            Dispatcher.UIThread.Post(() =>
            {
                var tb = FindNamed<TextBox>(control, "PART_TextBox");
                if (tb is not null)
                {
                    tb.TextAlignment = TextAlignment.Center;
                    tb.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                }

                var inc = FindNamed<RepeatButton>(control, "PART_IncreaseButton");
                var dec = FindNamed<RepeatButton>(control, "PART_DecreaseButton");
                if (inc is null || dec is null)
                {
                    return;
                }

                SetGlyph(inc, "+");
                SetGlyph(dec, "−");
            }, DispatcherPriority.Render);
            return;
        }

        SetGlyph(increase, "+");
        SetGlyph(decrease, "−");
    }

    private static void SetGlyph(RepeatButton button, string glyph)
    {
        if (button.Content is TextBlock existing && existing.Text == glyph)
        {
            return;
        }

        button.Content = new TextBlock
        {
            Text = glyph,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        button.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        button.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    private static T? FindNamed<T>(Visual root, string name) where T : class
    {
        foreach (var visual in root.GetVisualDescendants())
        {
            if (visual is StyledElement { Name: { } n } && n == name && visual is T match)
            {
                return match;
            }
        }

        return null;
    }
}
