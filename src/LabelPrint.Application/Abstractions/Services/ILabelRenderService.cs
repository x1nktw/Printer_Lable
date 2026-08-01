using LabelPrint.Domain.Templates;
using LabelPrint.Plugins.Abstractions.Printing;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Renders a template document to a rasterized label payload.
/// </summary>
public interface ILabelRenderService
{
    /// <summary>Renders the template with resolved variable values to PNG bytes.</summary>
    Task<RenderedLabel> RenderAsync(
        TemplateDocument document,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default);
}
