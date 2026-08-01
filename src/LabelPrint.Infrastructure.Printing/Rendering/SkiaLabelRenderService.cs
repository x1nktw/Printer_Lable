using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;
using LabelPrint.Plugins.Abstractions.Printing;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace LabelPrint.Infrastructure.Printing.Rendering;

/// <summary>
/// SkiaSharp-based label renderer for template documents.
/// </summary>
public sealed class SkiaLabelRenderService : ILabelRenderService
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+(?:\.\w+)*)\}\}", RegexOptions.Compiled);

    /// <inheritdoc />
    public Task<RenderedLabel> RenderAsync(
        TemplateDocument document,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        var dpi = document.Canvas.Dpi <= 0 ? 203 : document.Canvas.Dpi;
        var widthMm = document.Canvas.WidthMm <= 0 ? 40 : document.Canvas.WidthMm;
        var heightMm = document.Canvas.HeightMm <= 0 ? 58 : document.Canvas.HeightMm;
        var widthPx = MmToPx(widthMm, dpi);
        var heightPx = MmToPx(heightMm, dpi);

        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        foreach (var element in document.Elements.Where(e => e.IsVisible).OrderBy(e => e.Z))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DrawElement(canvas, element, variables, dpi);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();

        var rendered = new RenderedLabel(bytes, "image/png", widthMm, heightMm, dpi);
        return Task.FromResult(rendered);
    }

    private static void DrawElement(
        SKCanvas canvas,
        TemplateElementDocument element,
        IReadOnlyDictionary<string, string> variables,
        int dpi)
    {
        var x = MmToPx(element.Bounds.X, dpi);
        var y = MmToPx(element.Bounds.Y, dpi);
        var width = MmToPx(element.Bounds.Width, dpi);
        var height = MmToPx(element.Bounds.Height, dpi);

        canvas.Save();
        if (element.Rotation != 0)
        {
            canvas.RotateDegrees((float)element.Rotation, x + (width / 2f), y + (height / 2f));
        }

        switch (element.Type)
        {
            case TemplateElementType.Text when IsAddonsKitchenBinding(element):
                DrawAddonsKitchen(canvas, element, variables, x, y, width, height, dpi);
                break;
            case TemplateElementType.Text:
                DrawText(canvas, element, variables, x, y, width, height, dpi);
                break;
            case TemplateElementType.Image:
                DrawImage(canvas, element, x, y, width, height);
                break;
            case TemplateElementType.Barcode:
            case TemplateElementType.QrCode:
                DrawBarcode(canvas, element, variables, x, y, width, height);
                break;
            case TemplateElementType.Rectangle:
            case TemplateElementType.Ellipse:
                DrawShape(canvas, element, x, y, width, height, dpi);
                break;
            case TemplateElementType.Line:
                DrawLine(canvas, element, x, y, width, height, dpi);
                break;
        }

        canvas.Restore();
    }

    private static bool IsAddonsKitchenBinding(TemplateElementDocument element) =>
        element.BindingMode == TextBindingMode.Variable
        && string.Equals(element.ValueBinding, "AddonsKitchen", StringComparison.OrdinalIgnoreCase);

    private static void DrawAddonsKitchen(
        SKCanvas canvas,
        TemplateElementDocument element,
        IReadOnlyDictionary<string, string> variables,
        float x,
        float y,
        float width,
        float height,
        int dpi)
    {
        var raw = LookupVariable(variables, "Addons");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = LookupVariable(variables, "AddonsKitchen");
        }

        var addons = raw
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.TrimStart('•', '-', ' '))
            .Where(a => !string.IsNullOrWhiteSpace(a)
                        && !a.StartsWith("ДОБАВКИ", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (addons.Count == 0)
        {
            return;
        }

        var iconKeysRaw = LookupVariable(variables, "AddonIconKeys");
        var iconKeys = string.IsNullOrWhiteSpace(iconKeysRaw)
            ? Array.Empty<string>()
            : iconKeysRaw.Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.None)
                .Select(s => s.Trim())
                .ToArray();

        var font = element.Font ?? new TemplateFont { Family = "Inter", SizePt = 8, Bold = true };
        var typeface = LabelAssets.ResolveTypeface(font.Family, font.Bold);
        var titleSize = (float)(Math.Max(8, font.SizePt) * dpi / 72d);
        var rowSize = (float)(Math.Max(7, font.SizePt - 0.5) * dpi / 72d);
        using var titleFont = new SKFont(typeface, titleSize);
        using var rowFont = new SKFont(typeface, rowSize);
        using var paint = new SKPaint
        {
            Color = element.Invert ? SKColors.White : SKColors.Black,
            IsAntialias = true
        };

        var cursorY = y + titleSize;
        canvas.DrawText("ДОБАВКИ:", x, cursorY, SKTextAlign.Left, titleFont, paint);
        cursorY += titleSize * 0.35f;

        using (var linePaint = new SKPaint
        {
            Color = paint.Color,
            IsAntialias = true,
            StrokeWidth = Math.Max(1f, (float)(0.35 * dpi / 25.4)),
            Style = SKPaintStyle.Stroke
        })
        {
            canvas.DrawLine(x, cursorY, x + width, cursorY, linePaint);
        }

        cursorY += rowSize * 0.55f;
        var iconSize = MmToPx(3.2, dpi);
        var rowHeight = Math.Max(iconSize + 2, rowSize * 1.55f);
        var bottom = y + height;

        for (var i = 0; i < addons.Count; i++)
        {
            if (cursorY + rowHeight > bottom)
            {
                break;
            }

            var addon = addons[i];
            var iconKey = i < iconKeys.Length && !string.IsNullOrWhiteSpace(iconKeys[i])
                ? iconKeys[i]
                : LabelAssets.ResolveAddonIconKey(addon);
            using var icon = LabelAssets.TryLoadIcon(iconKey);
            if (icon is not null)
            {
                canvas.DrawBitmap(icon, new SKRect(x, cursorY, x + iconSize, cursorY + iconSize));
            }

            var textX = x + iconSize + MmToPx(1.2, dpi);
            var textWidth = Math.Max(1f, width - (textX - x));
            var lines = WrapText(addon, rowFont, textWidth);
            var textY = cursorY + rowSize;
            foreach (var line in lines.Take(2))
            {
                canvas.DrawText(line, textX, textY, SKTextAlign.Left, rowFont, paint);
                textY += rowSize * 1.1f;
            }

            cursorY += rowHeight;
            if (i < addons.Count - 1 && cursorY + 2 < bottom)
            {
                using var dash = new SKPaint
                {
                    Color = paint.Color,
                    IsAntialias = true,
                    StrokeWidth = Math.Max(1f, (float)(0.2 * dpi / 25.4)),
                    PathEffect = SKPathEffect.CreateDash([5f, 4f], 0f)
                };
                canvas.DrawLine(x, cursorY, x + width, cursorY, dash);
                cursorY += rowSize * 0.35f;
            }
        }
    }

    private static void DrawImage(
        SKCanvas canvas,
        TemplateElementDocument element,
        float x,
        float y,
        float width,
        float height)
    {
        if (string.IsNullOrWhiteSpace(element.ImagePath))
        {
            return;
        }

        using var bitmap = LabelAssets.TryLoadIcon(element.ImagePath);
        if (bitmap is null)
        {
            if (File.Exists(element.ImagePath))
            {
                using var fileBmp = SKBitmap.Decode(element.ImagePath);
                if (fileBmp is not null)
                {
                    canvas.DrawBitmap(fileBmp, new SKRect(x, y, x + width, y + height));
                }
            }

            return;
        }

        canvas.DrawBitmap(bitmap, new SKRect(x, y, x + width, y + height));
    }

    private static void DrawText(
        SKCanvas canvas,
        TemplateElementDocument element,
        IReadOnlyDictionary<string, string> variables,
        float x,
        float y,
        float width,
        float height,
        int dpi)
    {
        var text = ResolveText(element, variables);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var font = element.Font ?? new TemplateFont();
        var typeface = LabelAssets.ResolveTypeface(font.Family, font.Bold);
        var textSize = (float)(font.SizePt * dpi / 72d);
        using var skFont = new SKFont(typeface, textSize);
        using var paint = new SKPaint
        {
            Color = element.Invert ? SKColors.White : SKColors.Black,
            IsAntialias = true
        };

        var lineHeight = textSize * 1.15f;
        var lines = WrapText(text, skFont, Math.Max(1f, width));
        var maxLines = Math.Max(1, (int)Math.Floor(height / Math.Max(1f, lineHeight)));
        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
        }

        var skAlign = font.HorizontalAlign switch
        {
            TextHorizontalAlign.Center => SKTextAlign.Center,
            TextHorizontalAlign.Right => SKTextAlign.Right,
            _ => SKTextAlign.Left
        };

        var drawX = skAlign switch
        {
            SKTextAlign.Center => x + width / 2f,
            SKTextAlign.Right => x + width,
            _ => x
        };

        var blockHeight = lines.Count * lineHeight;
        var drawY = font.VerticalAlign switch
        {
            TextVerticalAlign.Middle => y + Math.Max(0f, (height - blockHeight) / 2f) + textSize,
            TextVerticalAlign.Bottom => y + Math.Max(0f, height - blockHeight) + textSize,
            _ => y + textSize
        };

        foreach (var line in lines)
        {
            canvas.DrawText(line, drawX, drawY, skAlign, skFont, paint);
            drawY += lineHeight;
            if (drawY - y > height + textSize * 0.25f)
            {
                break;
            }
        }
    }

    private static List<string> WrapText(string text, SKFont font, float maxWidth)
    {
        var result = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                result.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (font.MeasureText(candidate) <= maxWidth || string.IsNullOrEmpty(current))
                {
                    current = candidate;
                }
                else
                {
                    result.Add(current);
                    current = word;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                result.Add(current);
            }
        }

        return result;
    }

    private static void DrawBarcode(
        SKCanvas canvas,
        TemplateElementDocument element,
        IReadOnlyDictionary<string, string> variables,
        float x,
        float y,
        float width,
        float height)
    {
        var payload = ResolveText(element, variables);
        if (string.IsNullOrWhiteSpace(payload))
        {
            payload = "000000000000";
        }

        var format = MapSymbology(element);
        using var bitmap = GenerateBarcode(payload, format, Math.Max(1, (int)width), Math.Max(1, (int)height));
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, new SKRect(x, y, x + width, y + height));
    }

    private static SKBitmap GenerateBarcode(string payload, BarcodeFormat format, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 1,
                PureBarcode = true
            }
        };

        var pixelData = writer.Write(payload);
        var bitmap = new SKBitmap(pixelData.Width, pixelData.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var pixels = bitmap.GetPixels();
        Marshal.Copy(pixelData.Pixels, 0, pixels, pixelData.Pixels.Length);
        return bitmap;
    }

    private static void DrawShape(
        SKCanvas canvas,
        TemplateElementDocument element,
        float x,
        float y,
        float width,
        float height,
        int dpi)
    {
        using var paint = new SKPaint
        {
            Color = element.Invert ? SKColors.White : SKColors.Black,
            IsAntialias = true,
            Style = element.Filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1f, (float)(element.StrokeThickness * dpi / 25.4))
        };

        var rect = new SKRect(x, y, x + width, y + height);
        if (element.Type == TemplateElementType.Ellipse)
        {
            canvas.DrawOval(rect, paint);
            return;
        }

        var radius = MmToPx(element.CornerRadiusMm, dpi);
        if (radius > 0)
        {
            canvas.DrawRoundRect(rect, radius, radius, paint);
        }
        else
        {
            canvas.DrawRect(rect, paint);
        }
    }

    private static void DrawLine(
        SKCanvas canvas,
        TemplateElementDocument element,
        float x,
        float y,
        float width,
        float height,
        int dpi)
    {
        using var paint = new SKPaint
        {
            Color = element.Invert ? SKColors.White : SKColors.Black,
            IsAntialias = true,
            StrokeWidth = Math.Max(1f, (float)(element.StrokeThickness * dpi / 25.4)),
            PathEffect = element.Dashed
                ? SKPathEffect.CreateDash([6f, 4f], 0f)
                : null
        };

        canvas.DrawLine(x, y, x + width, y + height, paint);
    }

    private static string ResolveText(TemplateElementDocument element, IReadOnlyDictionary<string, string> variables)
    {
        return element.BindingMode switch
        {
            TextBindingMode.Variable when !string.IsNullOrWhiteSpace(element.ValueBinding) =>
                LookupVariable(variables, element.ValueBinding),
            TextBindingMode.CurrentDate =>
                !string.IsNullOrWhiteSpace(LookupVariable(variables, "Date"))
                    ? LookupVariable(variables, "Date")
                    : DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            TextBindingMode.CurrentTime =>
                !string.IsNullOrWhiteSpace(LookupVariable(variables, "Time"))
                    ? LookupVariable(variables, "Time")
                    : DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            _ => ReplacePlaceholders(element.Content ?? string.Empty, variables)
        };
    }

    private static string ReplacePlaceholders(string content, IReadOnlyDictionary<string, string> variables)
    {
        return PlaceholderRegex.Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            return LookupVariable(variables, key);
        });
    }

    private static string LookupVariable(IReadOnlyDictionary<string, string> variables, string key)
    {
        if (variables.TryGetValue(key, out var value))
        {
            return value;
        }

        return string.Empty;
    }

    private static BarcodeFormat MapSymbology(TemplateElementDocument element)
    {
        return element.Type == TemplateElementType.QrCode || element.Symbology == BarcodeSymbology.QrCode
            ? BarcodeFormat.QR_CODE
            : element.Symbology switch
            {
                BarcodeSymbology.Ean8 => BarcodeFormat.EAN_8,
                BarcodeSymbology.Code39 => BarcodeFormat.CODE_39,
                BarcodeSymbology.DataMatrix => BarcodeFormat.DATA_MATRIX,
                BarcodeSymbology.Ean13 => BarcodeFormat.EAN_13,
                _ => BarcodeFormat.CODE_128
            };
    }

    private static int MmToPx(double mm, int dpi) =>
        Math.Max(1, (int)Math.Round(mm * dpi / 25.4d, MidpointRounding.AwayFromZero));
}
