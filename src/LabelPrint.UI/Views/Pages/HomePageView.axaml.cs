using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LabelPrint.UI.Views.Pages;

public partial class HomePageView : UserControl
{
    public HomePageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
