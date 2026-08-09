using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace LabelPrint.UI.Controls;

/// <summary>
/// Sizes <see cref="NumericUpDown"/> to fit its formatted value / range instead of a fixed width.
/// </summary>
public static class AdaptiveNumericWidth
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<NumericUpDown, bool>(
            "IsEnabled",
            typeof(AdaptiveNumericWidth),
            defaultValue: false);

    private static readonly AttachedProperty<bool> HookedProperty =
        AvaloniaProperty.RegisterAttached<NumericUpDown, bool>(
            "Hooked",
            typeof(AdaptiveNumericWidth));

    static AdaptiveNumericWidth()
    {
        IsEnabledProperty.Changed.AddClassHandler<NumericUpDown>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(NumericUpDown element) =>
        element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(NumericUpDown element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(NumericUpDown control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.GetNewValue<bool>())
        {
            if (!control.GetValue(HookedProperty))
            {
                control.SetValue(HookedProperty, true);
                control.PropertyChanged += OnControlPropertyChanged;
                control.AttachedToVisualTree += (_, _) => ScheduleUpdate(control);
            }

            ScheduleUpdate(control);
            return;
        }

        if (control.GetValue(HookedProperty))
        {
            control.PropertyChanged -= OnControlPropertyChanged;
            control.SetValue(HookedProperty, false);
        }
    }

    private static void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not NumericUpDown control)
        {
            return;
        }

        if (e.Property == NumericUpDown.ValueProperty
            || e.Property == NumericUpDown.MinimumProperty
            || e.Property == NumericUpDown.MaximumProperty
            || e.Property == NumericUpDown.FormatStringProperty
            || e.Property == NumericUpDown.FontSizeProperty
            || e.Property == NumericUpDown.FontFamilyProperty
            || e.Property == NumericUpDown.FontWeightProperty
            || e.Property == NumericUpDown.ShowButtonSpinnerProperty
            || e.Property == NumericUpDown.PaddingProperty)
        {
            ScheduleUpdate(control);
        }
    }

    private static void ScheduleUpdate(NumericUpDown control) =>
        Dispatcher.UIThread.Post(() => UpdateWidth(control), DispatcherPriority.Loaded);

    private static void UpdateWidth(NumericUpDown control)
    {
        if (!GetIsEnabled(control) || !control.IsEffectivelyVisible)
        {
            return;
        }

        if (control.ShowButtonSpinner == false && control.BorderThickness == default)
        {
            return;
        }

        var typeface = new Typeface(
            control.FontFamily,
            control.FontStyle,
            control.FontWeight,
            control.FontStretch);
        var samples = BuildSamples(control);
        double textWidth = 0;
        foreach (var sample in samples)
        {
            var formatted = new FormattedText(
                sample,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                control.FontSize,
                Brushes.Black);
            textWidth = Math.Max(textWidth, formatted.Width);
        }

        var padding = control.Padding;
        var chrome = padding.Left + padding.Right + 12; // border + inner gap
        if (control.ShowButtonSpinner)
        {
            chrome += 34; // spinner column
        }

        var width = Math.Ceiling(textWidth + chrome);
        width = Math.Clamp(width, 72, 220);
        if (Math.Abs(control.Width - width) > 0.5)
        {
            control.Width = width;
        }

        control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        control.MinWidth = 0;
    }

    private static IEnumerable<string> BuildSamples(NumericUpDown control)
    {
        var format = string.IsNullOrWhiteSpace(control.FormatString)
            ? "0.##"
            : control.FormatString;

        var min = control.Minimum;
        var max = control.Maximum;
        var value = control.Value ?? 0;

        // Unbounded / huge ranges: size from current value with typing headroom.
        // decimal.MaxValue - decimal.MinValue overflows — never subtract unbounded ends.
        if (IsHugeRange(min, max))
        {
            var magnitude = Math.Max(Math.Abs(value), 100m);
            yield return Format(magnitude * 100m, format);
            yield return Format(value, format);
            yield return Format(-magnitude, format);
            yield break;
        }

        yield return Format(min, format);
        yield return Format(max, format);
        yield return Format(value, format);
        // Extra digit while typing toward the upper bound.
        yield return Format(max, format) + "0";
    }

    private static bool IsHugeRange(decimal min, decimal max)
    {
        if (min <= -1_000_000_000m || max >= 1_000_000_000m)
        {
            return true;
        }

        try
        {
            return max - min > 1_000_000m;
        }
        catch (OverflowException)
        {
            return true;
        }
    }

    private static string Format(decimal value, string format)
    {
        try
        {
            return value.ToString(format, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch
        {
            return value.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }
    }
}
