using System.Text.Json;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.Templates;
using LabelPrint.Domain.Entities;
using LabelPrint.Plugins.Abstractions.Variables;
using SkiaSharp;

namespace LabelPrint.Infrastructure.Printing.Services;

/// <summary>
/// Exports template JSON and rendered product labels (PNG/PDF).
/// </summary>
public sealed class ExportService : IExportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILabelRenderService _renderService;
    private readonly IVariableResolver _variableResolver;

    public ExportService(
        IUnitOfWork unitOfWork,
        ILabelRenderService renderService,
        IVariableResolver variableResolver)
    {
        _unitOfWork = unitOfWork;
        _renderService = renderService;
        _variableResolver = variableResolver;
    }

    /// <inheritdoc />
    public async Task<Result<string>> ExportTemplateJsonAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.Templates.GetByIdAsync(templateId, cancellationToken);
        if (template is null || template.IsArchived)
        {
            return Result.Failure<string>("Template not found.");
        }

        return Result.Success(template.ContentJson);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> RenderProductLabelPngAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var context = await BuildRenderContextAsync(productId, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<byte[]>(context.Error ?? "Failed to build render context.");
        }

        var rendered = await _renderService.RenderAsync(context.Value.Document, context.Value.Variables, cancellationToken);
        return Result.Success(rendered.Payload);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> RenderProductLabelPdfAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var pngResult = await RenderProductLabelPngAsync(productId, cancellationToken);
        if (pngResult.IsFailure)
        {
            return Result.Failure<byte[]>(pngResult.Error ?? "Failed to render PNG label.");
        }

        var context = await BuildRenderContextAsync(productId, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<byte[]>(context.Error ?? "Failed to build render context.");
        }

        var widthMm = context.Value.Document.Canvas.WidthMm;
        var heightMm = context.Value.Document.Canvas.HeightMm;
        var widthPt = MmToPoints(widthMm);
        var heightPt = MmToPoints(heightMm);

        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
        {
            Title = "LabelPrint Pro export",
            Author = "LabelPrint Pro"
        }))
        {
            using var canvas = document.BeginPage(widthPt, heightPt);
            canvas.Clear(SKColors.White);

            using var image = SKImage.FromEncodedData(pngResult.Value);
            if (image is not null)
            {
                var dest = SKRect.Create(0, 0, widthPt, heightPt);
                canvas.DrawImage(image, dest);
            }

            document.EndPage();
            document.Close();
        }

        return Result.Success(stream.ToArray());
    }

    private async Task<Result<RenderContext>> BuildRenderContextAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId, cancellationToken);
        if (product is null || product.IsArchived)
        {
            return Result.Failure<RenderContext>("Product not found.");
        }

        var template = await ResolveTemplateAsync(product, cancellationToken);
        if (template is null)
        {
            return Result.Failure<RenderContext>("No label template available.");
        }

        var variableContext = new VariableContext
        {
            ProductId = productId,
            Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PriceAmount"] = product.PriceAmount.ToString("0.##"),
                ["Currency"] = product.PriceCurrency
            }
        };

        var variables = await _variableResolver.ResolveAllAsync(variableContext, cancellationToken);
        var document = TemplateDocumentSerializer.Deserialize(template.ContentJson);
        document.TemplateId ??= template.Id.ToString();
        document.Name ??= template.Name;
        document.Canvas.WidthMm = template.WidthMm;
        document.Canvas.HeightMm = template.HeightMm;
        document.Canvas.Dpi = template.Dpi;

        return Result.Success(new RenderContext(document, variables));
    }

    private async Task<LabelTemplate?> ResolveTemplateAsync(Product product, CancellationToken cancellationToken)
    {
        if (product.DefaultTemplateId is Guid templateId)
        {
            var template = await _unitOfWork.Templates.GetByIdAsync(templateId, cancellationToken);
            if (template is not null && !template.IsArchived)
            {
                return template;
            }
        }

        var search = await _unitOfWork.Templates.SearchAsync(null, includeArchived: false, skip: 0, take: 1, cancellationToken);
        return search.Items.FirstOrDefault();
    }

    private static float MmToPoints(double mm) => (float)(mm * 72.0 / 25.4);

    private sealed record RenderContext(Domain.Templates.TemplateDocument Document, IReadOnlyDictionary<string, string> Variables);
}
