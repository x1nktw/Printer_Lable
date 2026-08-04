using FluentValidation;
using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Services;
using LabelPrint.Application.Variables;
using LabelPrint.Plugins.Abstractions.Variables;
using Microsoft.Extensions.DependencyInjection;

namespace LabelPrint.Application.DependencyInjection;

/// <summary>
/// Registers Application-layer services.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds application services and validators. Persistence ports are registered by Infrastructure.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IOrderFeedNotifier, OrderFeedNotifier>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUserSession, UserSession>();
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly);
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<IProductCsvService, ProductCsvService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<IPrinterService, PrinterService>();
        services.AddScoped<IPrintService, PrintService>();
        services.AddScoped<IPrintJobProcessor, PrintService>();
        services.AddScoped<IPrintQueueService, PrintQueueService>();
        services.AddScoped<IPrintHistoryService, PrintHistoryService>();
        services.AddScoped<OrderMatchingService>();
        services.AddScoped<OrderSyncService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ILabelDateTimeService, LabelDateTimeService>();
        services.AddScoped<IAddonService, AddonService>();
        services.AddScoped<IAddonIconResolver, AddonIconResolver>();
        services.AddSingleton<Queue.PrintQueueWorker>();
        services.AddSingleton<Queue.KitchenOrderPollWorker>();
        services.AddScoped<IVariableResolver, VariableResolver>();
        services.AddScoped<IVariableProvider, ProductNameVariableProvider>();
        services.AddScoped<IVariableProvider, SkuVariableProvider>();
        services.AddScoped<IVariableProvider, BarcodeVariableProvider>();
        services.AddScoped<IVariableProvider, PriceVariableProvider>();
        services.AddScoped<IVariableProvider, DateVariableProvider>();
        services.AddScoped<IVariableProvider, TimeVariableProvider>();
        services.AddScoped<IVariableProvider, DateTimeVariableProvider>();
        services.AddScoped<IVariableProvider, ManufacturedAtVariableProvider>();
        services.AddScoped<IVariableProvider, ExpireDateVariableProvider>();
        services.AddScoped<IVariableProvider, ExpireTimeVariableProvider>();
        services.AddScoped<IVariableProvider, TemperatureRegimeVariableProvider>();
        services.AddScoped<IVariableProvider, ProductIconKeyVariableProvider>();
        services.AddScoped<IVariableProvider, OrderNumberVariableProvider>();
        services.AddScoped<IVariableProvider, PositionNameVariableProvider>();
        services.AddScoped<IVariableProvider, PositionIndexVariableProvider>();
        services.AddScoped<IVariableProvider, PositionTotalVariableProvider>();
        return services;
    }
}
