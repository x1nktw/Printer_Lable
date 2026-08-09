using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LabelPrint.UI.Controls;

public partial class AppTimeBox : UserControl
{
    public static readonly StyledProperty<TimeSpan?> SelectedTimeProperty =
        AvaloniaProperty.Register<AppTimeBox, TimeSpan?>(
            nameof(SelectedTime),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private int _hours;
    private int _minutes;

    public AppTimeBox()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    public TimeSpan? SelectedTime
    {
        get => GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedTimeProperty)
        {
            UpdateDisplay();
        }
    }

    private void OnChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var time = SelectedTime ?? DateTime.Now.TimeOfDay;
        _hours = time.Hours;
        _minutes = time.Minutes;
        RefreshEditorTexts();
        EditorPopup.IsOpen = true;
        e.Handled = true;
    }

    private void OnStepClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not RepeatButton { Tag: string tag })
        {
            return;
        }

        switch (tag)
        {
            case "hour:+1":
                StepHours(1);
                break;
            case "hour:-1":
                StepHours(-1);
                break;
            case "minute:+1":
                StepMinutes(1);
                break;
            case "minute:-1":
                StepMinutes(-1);
                break;
        }
    }

    private void StepHours(int delta) =>
        SetEditorTime(Wrap(_hours + delta, 24), _minutes);

    private void StepMinutes(int delta) =>
        SetEditorTime(_hours, Wrap(_minutes + delta, 60));

    private void SetEditorTime(int hours, int minutes)
    {
        _hours = hours;
        _minutes = minutes;
        RefreshEditorTexts();
    }

    private void RefreshEditorTexts()
    {
        if (HourText is not null)
        {
            HourText.Text = _hours.ToString("00");
        }

        if (MinuteText is not null)
        {
            MinuteText.Text = _minutes.ToString("00");
        }
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        SelectedTime = new TimeSpan(_hours, _minutes, 0);
        EditorPopup.IsOpen = false;
    }

    private void UpdateDisplay()
    {
        if (DisplayText is null)
        {
            return;
        }

        DisplayText.Text = SelectedTime is { } t
            ? $"{t.Hours:00}:{t.Minutes:00}"
            : "--:--";
    }

    private static int Wrap(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
