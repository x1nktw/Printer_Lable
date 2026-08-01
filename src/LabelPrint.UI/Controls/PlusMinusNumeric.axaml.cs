using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LabelPrint.UI.Controls;

public partial class PlusMinusNumeric : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<PlusMinusNumeric, double>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<PlusMinusNumeric, double>(nameof(Minimum), double.MinValue);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<PlusMinusNumeric, double>(nameof(Maximum), double.MaxValue);

    public static readonly StyledProperty<double> IncrementProperty =
        AvaloniaProperty.Register<PlusMinusNumeric, double>(nameof(Increment), 1);

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<PlusMinusNumeric, string>(nameof(FormatString), "0.##");

    public static readonly StyledProperty<double> FieldWidthProperty =
        AvaloniaProperty.Register<PlusMinusNumeric, double>(nameof(FieldWidth), 72);

    public PlusMinusNumeric()
    {
        InitializeComponent();
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    public double FieldWidth
    {
        get => GetValue(FieldWidthProperty);
        set => SetValue(FieldWidthProperty, value);
    }

    private void OnMinusClick(object? sender, RoutedEventArgs e) => Step(-Increment);

    private void OnPlusClick(object? sender, RoutedEventArgs e) => Step(Increment);

    private void Step(double delta)
    {
        Value = Math.Clamp(Value + delta, Minimum, Maximum);
    }
}
