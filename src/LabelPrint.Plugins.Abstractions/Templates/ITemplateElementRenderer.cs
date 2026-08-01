using LabelPrint.Domain.Templates;

namespace LabelPrint.Plugins.Abstractions.Templates;

/// <summary>
/// Renders a single template element onto a label surface.
/// </summary>
public interface ITemplateElementRenderer
{
    /// <summary>Element type this renderer supports.</summary>
    string ElementType { get; }

    /// <summary>Draws the element into the provided render target.</summary>
    Task RenderAsync(TemplateElementDocument element, ElementRenderContext context, CancellationToken cancellationToken = default);
}

/// <summary>Context passed to element renderers.</summary>
public sealed class ElementRenderContext
{
    public required IReadOnlyDictionary<string, string> ResolvedVariables { get; init; }

    public required double Dpi { get; init; }

    public required object Target { get; init; }
}

/// <summary>
/// Migrates template JSON documents between schema versions.
/// </summary>
public interface ITemplateSchemaMigrator
{
    /// <summary>Highest schema version this migrator can produce.</summary>
    int TargetSchemaVersion { get; }

    /// <summary>Migrates raw JSON to the target schema version.</summary>
    string Migrate(string contentJson, int fromSchemaVersion);
}
