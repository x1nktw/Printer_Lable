using System;
using System.Threading.Tasks;
using Avalonia;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DependencyInjection;
using LabelPrint.Application.Options;
using LabelPrint.Application.Queue;
using LabelPrint.Infrastructure.DependencyInjection;
using LabelPrint.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Velopack;

namespace LabelPrint.UI;

sealed class Program
{
    public static IServiceProvider Services { get; private set; } = null!;
    private static PrintQueueWorker? _queueWorker;
    private static KitchenOrderPollWorker? _kitchenPollWorker;
    private static IOrderWebhookHost? _webhookService;

    [STAThread]
    public static void Main(string[] args)
    {
        // Must run before any other startup logic (handles Velopack hooks / pending updates).
        VelopackApp.Build().Run();

        try
        {
            var configuration = BuildConfiguration();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.Configure<PrintQueueOptions>(configuration.GetSection(PrintQueueOptions.SectionName));
            services.AddApplication();
            services.AddInfrastructure(configuration);
            services.AddSingleton<ViewModels.MainViewModel>();
            services.AddTransient<ViewModels.CatalogViewModel>();
            services.AddTransient<ViewModels.RawMaterialsViewModel>();
            services.AddTransient<ViewModels.PrintersViewModel>();
            services.AddTransient<ViewModels.QueueViewModel>();
            services.AddTransient<ViewModels.HistoryViewModel>();
            services.AddSingleton<ViewModels.OrdersViewModel>();
            services.AddTransient<ViewModels.SettingsViewModel>();
            services.AddSingleton<LabelPrint.UI.Services.IUiDialogService, LabelPrint.UI.Services.AvaloniaUiDialogService>();
            Services = services.BuildServiceProvider();

            InitializeDatabaseAsync(Services).GetAwaiter().GetResult();
            StartBackgroundServices(Services);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "LabelPrint Pro terminated unexpectedly");
            throw;
        }
        finally
        {
            StopBackgroundServices();
            Log.CloseAndFlush();
        }
    }

    public static void StopBackgroundServices()
    {
        _kitchenPollWorker?.StopAsync().GetAwaiter().GetResult();
        _kitchenPollWorker = null;
        _queueWorker?.StopAsync().GetAwaiter().GetResult();
        _queueWorker = null;
        _webhookService?.Stop();
        _webhookService = null;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static IConfiguration BuildConfiguration()
    {
        var baseDir = AppContext.BaseDirectory;
        var configDir = Path.Combine(baseDir, "config");
        var contentRoot = File.Exists(Path.Combine(configDir, "appsettings.json"))
            ? configDir
            : baseDir;

        return new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
    }

    private static void StartBackgroundServices(IServiceProvider services)
    {
        _webhookService = services.GetRequiredService<IOrderWebhookHost>();
        _webhookService.Start();

        _kitchenPollWorker = services.GetRequiredService<KitchenOrderPollWorker>();
        _kitchenPollWorker.Start();

        var options = services.GetRequiredService<IOptions<PrintQueueOptions>>().Value;
        if (!options.UseBackgroundWorker)
        {
            return;
        }

        _queueWorker = services.GetRequiredService<PrintQueueWorker>();
        _queueWorker.Start();
    }
}
