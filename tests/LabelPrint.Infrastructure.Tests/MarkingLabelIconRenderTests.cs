using FluentAssertions;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;
using LabelPrint.Infrastructure.Printing.Rendering;
using SkiaSharp;

namespace LabelPrint.Infrastructure.Tests;

public class MarkingLabelIconRenderTests
{
    [Fact]
    public async Task Raw_Template_Renders_ProductIconKey()
    {
        var iconKey = $"test-marking-{Guid.NewGuid():N}";
        var iconPath = WriteSolidPngIcon(iconKey);
        try
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
                ["ProductIconKey"] = iconKey
            };

            var render = new SkiaLabelRenderService();
            var result = await render.RenderAsync(document, vars);
            result.Payload.Length.Should().BeGreaterThan(500);
            result.Payload[0].Should().Be(0x89);
        }
        finally
        {
            TryDelete(iconPath);
        }
    }

    [Fact]
    public async Task Raw_Template_Skips_Empty_ProductIconKey()
    {
        var iconKey = $"test-marking-skip-{Guid.NewGuid():N}";
        var iconPath = WriteSolidPngIcon(iconKey);
        try
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
                        // Static path must NOT be used when the variable is empty.
                        ImagePath = $"asset:icons/{iconKey}.png"
                    }
                ]
            };

            var render = new SkiaLabelRenderService();
            var withIcon = await render.RenderAsync(
                document,
                new Dictionary<string, string> { ["ProductIconKey"] = iconKey });
            var withoutIcon = await render.RenderAsync(
                document,
                new Dictionary<string, string> { ["ProductIconKey"] = "" });

            withIcon.Payload.Length.Should().BeGreaterThan(100);
            withoutIcon.Payload.Length.Should().BeGreaterThan(100);
            // Empty variable must not fall back to static ImagePath.
            withoutIcon.Payload.Length.Should().BeLessThan(withIcon.Payload.Length);
        }
        finally
        {
            TryDelete(iconPath);
        }
    }

    private static string WriteSolidPngIcon(string key)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "addon-icons");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{key}.png");

        using var bmp = new SKBitmap(32, 32);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Black);
        }

        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data!.ToArray());
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup for LocalAppData test artifact
        }
    }
}
