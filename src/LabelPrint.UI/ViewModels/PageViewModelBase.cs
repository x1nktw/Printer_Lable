using CommunityToolkit.Mvvm.ComponentModel;

namespace LabelPrint.UI.ViewModels;

public abstract partial class PageViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;
}
