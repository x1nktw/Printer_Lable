using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using LabelPrint.UI.ViewModels;

namespace LabelPrint.UI.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private bool _pageMotionReady;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) => _pageMotionReady = true;
        PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            UpdateMaximizeGlyph();
        }
    }

    private void UpdateMaximizeGlyph()
    {
        var maximized = WindowState == WindowState.Maximized;
        if (MaximizeGlyph is not null)
        {
            MaximizeGlyph.IsVisible = !maximized;
        }

        if (RestoreGlyph is not null)
        {
            RestoreGlyph.IsVisible = maximized;
        }

        if (MaximizeButton is not null)
        {
            ToolTip.SetTip(MaximizeButton, maximized ? "Свернуть в окно" : "Развернуть");
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as MainViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            _ = PlayPageEnterAsync();
        }
    }

    private async Task PlayPageEnterAsync()
    {
        if (!_pageMotionReady || PageHost is null)
        {
            return;
        }

        PageHost.Opacity = 0;
        PageHost.RenderTransform = TransformOperations.Parse("translateY(10px)");

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(16);

        PageHost.Opacity = 1;
        PageHost.RenderTransform = TransformOperations.Parse("none");
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
