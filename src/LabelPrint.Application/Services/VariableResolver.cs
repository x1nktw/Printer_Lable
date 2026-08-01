using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Plugins.Abstractions.Variables;

namespace LabelPrint.Application.Services;

/// <summary>
/// Resolves variables from explicit context values and registered <see cref="IVariableProvider"/> plugins.
/// </summary>
public sealed class VariableResolver : IVariableResolver
{
    private static readonly string[] KnownKeys =
    [
        "ProductName", "Name", "Sku", "Barcode", "Price", "PriceAmount", "Currency"
    ];

    private readonly IEnumerable<IVariableProvider> _providers;

    public VariableResolver(IEnumerable<IVariableProvider> providers) => _providers = providers;

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string key, VariableContext context, CancellationToken cancellationToken = default)
    {
        if (context.Values.TryGetValue(key, out var explicitValue))
        {
            return explicitValue;
        }

        if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveAsync("ProductName", context, cancellationToken);
        }

        var provider = _providers.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (provider is not null)
        {
            return await provider.ResolveAsync(context, cancellationToken);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> ResolveAllAsync(
        VariableContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in context.Values)
        {
            result[pair.Key] = pair.Value;
        }

        foreach (var key in KnownKeys)
        {
            if (result.ContainsKey(key))
            {
                continue;
            }

            var value = await ResolveAsync(key, context, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                result[key] = value;
            }
        }

        foreach (var provider in _providers)
        {
            if (result.ContainsKey(provider.Key))
            {
                continue;
            }

            var value = await provider.ResolveAsync(context, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                result[provider.Key] = value;
            }
        }

        return result;
    }
}
