using System.Reflection;
using System.Runtime.Loader;
using LabelPrint.Plugins.Abstractions.Orders;
using LabelPrint.Plugins.Abstractions.Templates;
using LabelPrint.Plugins.Abstractions.Variables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelPrint.Infrastructure.Plugins;

/// <summary>
/// Loads plugin assemblies from the <c>plugins/</c> folder using isolated <see cref="AssemblyLoadContext"/> instances.
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<PluginLoadContext> _contexts = [];

    public PluginLoader(ILogger<PluginLoader>? logger = null)
    {
        _logger = logger ?? NullLogger<PluginLoader>.Instance;
    }

    /// <summary>
    /// Scans <paramref name="pluginsDirectory"/> for DLLs and registers discovered plugin services into DI.
    /// Call during composition root setup (before <see cref="ServiceProvider"/> is built).
    /// </summary>
    public void RegisterPlugins(IServiceCollection services, string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            _logger.LogInformation("Plugins directory {PluginsDirectory} does not exist; creating empty folder.", pluginsDirectory);
            Directory.CreateDirectory(pluginsDirectory);
            return;
        }

        var dllPaths = Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("LabelPrint.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dllPaths.Count == 0)
        {
            _logger.LogInformation("No plugin DLLs found in {PluginsDirectory}.", pluginsDirectory);
            return;
        }

        var abstractionsAssembly = typeof(IVariableProvider).Assembly;

        foreach (var dllPath in dllPaths)
        {
            try
            {
                var context = new PluginLoadContext(Path.GetFullPath(dllPath), abstractionsAssembly);
                _contexts.Add(context);
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
                RegisterTypesFromAssembly(services, assembly, dllPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin assembly {PluginPath}", dllPath);
            }
        }
    }

    /// <summary>
    /// Unloads loaded plugin contexts. Safe to call on shutdown; optional for MVP.
    /// </summary>
    public void UnloadAll()
    {
        foreach (var context in _contexts)
        {
            try
            {
                context.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Plugin context unload failed.");
            }
        }

        _contexts.Clear();
    }

    private void RegisterTypesFromAssembly(IServiceCollection services, Assembly assembly, string sourcePath)
    {
        var exportedTypes = assembly.GetExportedTypes().Where(t => t.IsClass && !t.IsAbstract).ToList();
        var registered = 0;

        foreach (var type in exportedTypes)
        {
            if (typeof(IVariableProvider).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(IVariableProvider), type);
                registered++;
                _logger.LogInformation("Registered plugin variable provider {Type} from {Source}", type.FullName, sourcePath);
            }

            if (typeof(ITemplateElementRenderer).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(ITemplateElementRenderer), type);
                registered++;
                _logger.LogInformation("Registered plugin template renderer {Type} from {Source}", type.FullName, sourcePath);
            }

            if (typeof(IOrderProvider).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(IOrderProvider), type);
                registered++;
                _logger.LogInformation("Registered plugin order provider {Type} from {Source}", type.FullName, sourcePath);
            }
        }

        if (registered == 0)
        {
            _logger.LogDebug("Plugin assembly {Source} has no discoverable plugin types.", sourcePath);
        }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly Assembly _sharedAbstractions;

        public PluginLoadContext(string pluginPath, Assembly sharedAbstractions)
            : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
            _sharedAbstractions = sharedAbstractions;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == _sharedAbstractions.GetName().Name)
            {
                return _sharedAbstractions;
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
