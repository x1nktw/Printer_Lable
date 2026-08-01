using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Infrastructure.Plugins;

/// <summary>
/// Registers plugin loading from the application plugins folder.
/// </summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// Loads optional plugin assemblies from <paramref name="pluginsDirectory"/> (default: <c>plugins/</c> next to the app).
    /// </summary>
    public static (IServiceCollection Services, PluginLoader Loader) AddPluginInfrastructure(
        this IServiceCollection services,
        ILogger<PluginLoader>? logger = null,
        string? pluginsDirectory = null)
    {
        pluginsDirectory ??= Path.Combine(AppContext.BaseDirectory, "plugins");
        var loader = new PluginLoader(logger);
        loader.RegisterPlugins(services, pluginsDirectory);
        services.AddSingleton(loader);
        return (services, loader);
    }
}
