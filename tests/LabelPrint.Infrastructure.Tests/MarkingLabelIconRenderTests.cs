using FluentAssertions;
using LabelPrint.Application.Templates;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;
using LabelPrint.Infrastructure.Printing.Rendering;

namespace LabelPrint.Infrastructure.Tests;

public class MarkingLabelIconRenderTests
{
    [Fact]
    public async Task Raw_Template_Renders_ProductIconKey()
    {
        var document = new TemplateDocument
        {
            SchemaVersion = 1,
            Name = "Сырьё icon test",
            Canvas = new TemplateCanvas { WidthMm = 58, HeightMm = 40, Dpi = 203 },
            Elements =
            [
                new TemplateElementDocument
                {
                    Id = "name",
                    Type = TemplateElementType.Text,
                    Bounds = new TemplateBounds { X = 2, Y = 1, Width = 42, Height = 12 },
                    BindingMode = TextBindingMode.Variable,
                    ValueBinding = "ProductName",
                    Font = new TemplateFont { Family = "Arial", SizePt = 14, Bold = true }
                },
                new TemplateElementDocument
                {
                    Id = "icon",
                    Type = TemplateElementType.Image,
                    Bounds = new TemplateBounds { X = 46, Y = 1.5, Width = 10, Height = 10 },
                    BindingMode = TextBindingMode.Variable,
                    ValueBinding = "ProductIconKey"
                }
            ]
        };

        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductName"] = "Сыр",
            ["ProductIconKey"] = "cheese"
        };

        var render = new SkiaLabelRenderService();
        var result = await render.RenderAsync(document, vars);
        result.Payload.Length.Should().BeGreaterThan(500);
        result.Payload[0].Should().Be(0x89);
    }

    [Fact]
    public async Task Raw_Template_Skips_Empty_ProductIconKey()
    {
        var document = new TemplateDocument
        {
            SchemaVersion = 1,
            Name = "Сырьё empty icon",
            Canvas = new TemplateCanvas { WidthMm = 58, HeightMm = 40, Dpi = 203 },
            Elements =
            [
                new TemplateElementDocument
                {
                    Id = "icon",
                    Type = TemplateElementType.Image,
                    Bounds = new TemplateBounds { X = 46, Y = 1.5, Width = 10, Height = 10 },
                    BindingMode = TextBindingMode.Variable,
                    ValueBinding = "ProductIconKey",
                    ImagePath = "asset:icons/cheese.png"
                }
            ]
        };

        var render = new SkiaLabelRenderService();
        var withIcon = await render.RenderAsync(
            document,
            new Dictionary<string, string> { ["ProductIconKey"] = "cheese" });
        var withoutIcon = await render.RenderAsync(
            document,
            new Dictionary<string, string> { ["ProductIconKey"] = "" });

        withIcon.Payload.Length.Should().BeGreaterThan(100);
        withoutIcon.Payload.Length.Should().BeGreaterThan(100);
        // Empty variable must not fall back to static ImagePath.
        withoutIcon.Payload.Length.Should().BeLessThan(withIcon.Payload.Length);
    }
}
