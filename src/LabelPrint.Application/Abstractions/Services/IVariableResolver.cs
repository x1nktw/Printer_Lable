using LabelPrint.Plugins.Abstractions.Variables;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Resolves template variable placeholders for preview and print.
/// </summary>
public interface IVariableResolver
{
    /// <summary>Resolves a single variable key for the given context.</summary>
    Task<string?> ResolveAsync(string key, VariableContext context, CancellationToken cancellationToken = default);

    /// <summary>Builds a dictionary of all known variables for the context.</summary>
    Task<IReadOnlyDictionary<string, string>> ResolveAllAsync(
        VariableContext context,
        CancellationToken cancellationToken = default);
}
