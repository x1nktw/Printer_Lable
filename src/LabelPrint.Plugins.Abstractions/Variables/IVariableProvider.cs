namespace LabelPrint.Plugins.Abstractions.Variables;

/// <summary>
/// Resolves a named template variable during preview/print.
/// </summary>
public interface IVariableProvider
{
    /// <summary>Variable key without braces, e.g. ProductName.</summary>
    string Key { get; }

    /// <summary>Localized display name for the editor palette.</summary>
    string DisplayName { get; }

    /// <summary>Resolves the variable value for the given context.</summary>
    Task<string> ResolveAsync(VariableContext context, CancellationToken cancellationToken = default);
}

/// <summary>Context available to variable providers while rendering a label.</summary>
public sealed class VariableContext
{
    public Guid? ProductId { get; init; }

    public Guid? OrderId { get; init; }

    public Guid? OrderItemId { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
