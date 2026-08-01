using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Infrastructure.Printing.Gateways;
using LabelPrint.Infrastructure.Printing.Options;
using LabelPrint.Infrastructure.Printing.Rendering;
using LabelPrint.Infrastructure.Printing.Services;
using LabelPrint.Plugins.Abstractions.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace LabelPrint.Infrastructure.Printing.DependencyInjection;

/// <summary>
/// Registers printing adapters and label rendering.
/// </summary>
public static class PrintingServiceCollectionExtensions
{
    /// <summary>
    /// Adds printing gateways, renderer, and options.
    /// </summary>
    public static IServiceCollection AddPrintingInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<PrintingOptions>(configuration.GetSection(PrintingOptions.SectionName));
        }
        else
        {
            services.Configure<PrintingOptions>(_ => { });
        }

        services.AddScoped<ILabelRenderService, SkiaLabelRenderService>();
        services.AddScoped<IProtocolPrinterGateway, FilePrinterGateway>();
        if (OperatingSystem.IsWindows())
        {
            services.AddScoped<IProtocolPrinterGateway, WindowsPrintApiGateway>();
        }

        services.AddScoped<IProtocolPrinterGateway, TsplPrinterGateway>();
        services.AddScoped<IProtocolPrinterGateway, CpclPrinterGateway>();
        services.AddScoped<IProtocolPrinterGateway, EscPosPrinterGateway>();
        services.AddScoped<IPrinterGateway, CompositePrinterGateway>();
        services.AddScoped<IExportService, ExportService>();
        return services;
    }
}
