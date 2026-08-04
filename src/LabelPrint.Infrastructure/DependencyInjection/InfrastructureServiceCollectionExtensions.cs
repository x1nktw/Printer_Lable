using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Options;
using LabelPrint.Infrastructure.FrontPad.DependencyInjection;
using LabelPrint.Infrastructure.Persistence;
using LabelPrint.Infrastructure.Persistence.Repositories;
using LabelPrint.Infrastructure.Plugins;
using LabelPrint.Infrastructure.Printing.DependencyInjection;
using LabelPrint.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace LabelPrint.Infrastructure.DependencyInjection;

/// <summary>
/// Composition registrations for infrastructure adapters.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers logging, persistence, printing and FrontPad modules.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LabelPrintPro",
                    "logs",
                    "labelprint-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddDbContext<LabelPrintDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
            options.UseSqlite($"Data Source={dbOptions.ResolveDatabasePath()}");
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        // Gateways resolve IPrinterRepository directly; reuse the UnitOfWork instance for the scope.
        services.AddScoped<IPrinterRepository>(sp => sp.GetRequiredService<IUnitOfWork>().Printers);
        services.AddScoped<DatabaseInitializer>();

        services.AddPrintingInfrastructure(configuration);
        services.AddFrontPadInfrastructure();
        services.Configure<UpdateOptions>(configuration.GetSection(UpdateOptions.SectionName));
        services.AddSingleton<IUpdateChecker, VelopackUpdateChecker>();
        services.AddPluginInfrastructure();
        return services;
    }
}
