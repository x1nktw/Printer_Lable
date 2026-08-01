using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LabelPrint.UI.ViewModels;
using LabelPrint.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LabelPrint.UI;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Program.Services.GetRequiredService<MainViewModel>(),
            };
            desktop.Exit += (_, _) => Program.StopBackgroundServices();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
