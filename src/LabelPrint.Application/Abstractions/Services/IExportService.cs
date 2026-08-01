using LabelPrint.Application.Common;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Exports templates and rendered product labels to portable file formats.
/// </summary>
public interface IExportService
{
    /// <summary>Exports the template document JSON (including schemaVersion).</summary>
    Task<Result<string>> ExportTemplateJsonAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>Renders a product label to PNG bytes using the product's default template.</summary>
    Task<Result<byte[]>> RenderProductLabelPngAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Renders a product label to a single-page PDF (Skia PDF with embedded PNG).</summary>
    Task<Result<byte[]>> RenderProductLabelPdfAsync(Guid productId, CancellationToken cancellationToken = default);
}
