using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Infrastructure.FrontPad.Orders;
using LabelPrint.Plugins.Abstractions.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Infrastructure.FrontPad.DependencyInjection;

/// <summary>
/// Registers FrontPad inbox provider and webhook listener (no shop API).
/// </summary>
public static class FrontPadServiceCollectionExtensions
{
    /// <summary>
    /// Adds Bridge webhook + JSON inbox order adapters.
    /// </summary>
    public static IServiceCollection AddFrontPadInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<JsonFileOrderProvider>();
        services.AddSingleton<NullOrderProvider>();
        services.AddSingleton<CompositeOrderProvider>();
        services.AddSingleton<IOrderProvider>(sp => sp.GetRequiredService<CompositeOrderProvider>());
        services.AddSingleton<OrderWebhookListener>();
        services.AddSingleton<OrderWebhookHostedService>();
        services.AddSingleton<IOrderWebhookHost>(sp => sp.GetRequiredService<OrderWebhookHostedService>());

        return services;
    }
}

/// <summary>
/// Starts the webhook listener on app startup using settings from the database.
/// </summary>
public sealed class OrderWebhookHostedService : IOrderWebhookHost
{
    private readonly OrderWebhookListener _listener;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderWebhookHostedService> _logger;

    public OrderWebhookHostedService(
        OrderWebhookListener listener,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderWebhookHostedService> logger)
    {
        _listener = listener;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Starts webhook listener if configured.</summary>
    public void Start()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider
                .GetRequiredService<LabelPrint.Application.Abstractions.Repositories.IUnitOfWork>()
                .Settings.GetAsync().GetAwaiter().GetResult();
            var url = string.IsNullOrWhiteSpace(settings.FrontPadWebhookListenUrl)
                ? "http://127.0.0.1:8765/"
                : settings.FrontPadWebhookListenUrl;
            _listener.Start(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize order webhook listener");
        }
    }

    /// <summary>Stops webhook listener.</summary>
    public void Stop() => _listener.Stop();
}
