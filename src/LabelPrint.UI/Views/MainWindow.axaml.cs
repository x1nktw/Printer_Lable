using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
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
}
