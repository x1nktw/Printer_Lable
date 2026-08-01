using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LabelPrint.UI.Views.Pages;

public partial class CatalogPageView : UserControl
{
    public CatalogPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
