using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LabelPrint.UI.Views.Pages;

public partial class PlaceholderPageView : UserControl
{
    public PlaceholderPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
