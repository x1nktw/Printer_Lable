using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Templates;
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
        // Positions may be 0; line width/height may be 0 (true horizontal/vertical).
        // Other element sizes need at least 1px so text/shapes still draw.
        var x = MmToPx(element.Bounds.X, dpi, minPixels: 0);
        var y = MmToPx(element.Bounds.Y, dpi, minPixels: 0);
        var isLine = element.Type == TemplateElementType.Line;
        var width = MmToPx(element.Bounds.Width, dpi, minPixels: isLine ? 0 : 1);
        var height = MmToPx(element.Bounds.Height, dpi, minPixels: isLine ? 0 : 1);

        canvas.Save();
        if (element.Rotation != 0)
        {
            // Rotate around the geometric midpoint of the element (for lines: segment midpoint).
            var pivotX = x + width / 2f;
            var pivotY = y + height / 2f;
            canvas.RotateDegrees((float)element.Rotation, pivotX, pivotY);
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
                DrawImage(canvas, element, variables, x, y, width, height);
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

        var elementFont = element.Font ?? new TemplateFont { Family = "Inter", SizePt = 8, Bold = true };
        var layout = AddonsKitchenLayoutDefaults.Resolve(
            element.AddonsKitchen,
            elementFont,
            width / (dpi / 25.4f));

        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + width, y + height));

        if (addons.Count == 0)
        {
            foreach (var part in layout.EmptyElements.Where(p => p.Visible))
            {
                if (AddonsKitchenLayoutDefaults.IsImagePart(part))
                {
                    DrawAddonsKitchenImage(canvas, part, x, y, dpi);
                }
                else
                {
                    DrawAddonsKitchenText(canvas, part, element, x, y, dpi);
                }
            }

            canvas.Restore();
            return;
        }

        var iconKeysRaw = LookupVariable(variables, "AddonIconKeys");
        var iconKeys = string.IsNullOrWhiteSpace(iconKeysRaw)
            ? Array.Empty<string>()
            : iconKeysRaw.Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.None)
                .Select(s => s.Trim())
                .ToArray();

        var bottom = y + height;

        if (layout.Title is { Visible: true } title)
        {
            DrawAddonsKitchenText(canvas, title, element, x, y, dpi);
        }

        if (layout.Underline is { Visible: true } underline)
        {
            DrawAddonsKitchenLine(canvas, underline, element, x, y, dpi);
        }

        var stepMm = layout.RowHeightMm + layout.RowGapMm;
        for (var i = 0; i < addons.Count; i++)
        {
            var rowOriginYMm = layout.RowsOriginYMm + i * stepMm;
            var rowOriginY = y + MmToPx(rowOriginYMm, dpi);
            if (rowOriginY >= bottom)
            {
                break;
            }

            var rowBottom = rowOriginY + MmToPx(layout.RowHeightMm, dpi);
            if (rowBottom > bottom + 0.5f && i > 0)
            {
                break;
            }

            var addon = addons[i];
            var iconKey = i < iconKeys.Length && !string.IsNullOrWhiteSpace(iconKeys[i])
                ? iconKeys[i]
                : string.Empty;

            if (layout.Icon is { Visible: true } iconPart)
            {
                var ix = x + MmToPx(iconPart.Bounds.X, dpi);
                var iy = rowOriginY + MmToPx(iconPart.Bounds.Y, dpi);
                var iw = MmToPx(Math.Max(0.5, iconPart.Bounds.Width), dpi);
                var ih = MmToPx(Math.Max(0.5, iconPart.Bounds.Height), dpi);
                using var icon = string.IsNullOrWhiteSpace(iconKey) ? null : LabelAssets.TryLoadIcon(iconKey);
                if (icon is not null)
                {
                    canvas.DrawBitmap(icon, new SKRect(ix, iy, ix + iw, iy + ih));
                }
            }

            if (layout.Text is { Visible: true } textPart)
            {
                DrawAddonsKitchenRowText(
                    canvas,
                    textPart,
                    element,
                    addon,
                    x,
                    rowOriginY,
                    dpi);
            }

            if (i < addons.Count - 1 && layout.Separator is { Visible: true } separator)
            {
                var sepY = rowOriginY + MmToPx(separator.Bounds.Y, dpi);
                if (sepY + 2 < bottom)
                {
                    DrawAddonsKitchenLine(canvas, separator, element, x, rowOriginY, dpi);
                }
            }
        }

        canvas.Restore();
    }

    private static void DrawAddonsKitchenImage(
        SKCanvas canvas,
        AddonsKitchenPart part,
        float blockX,
        float blockY,
        int dpi)
    {
        if (string.IsNullOrWhiteSpace(part.ImagePath))
        {
            return;
        }

        using var icon = LabelAssets.TryLoadIcon(part.ImagePath);
        if (icon is null)
        {
            return;
        }

        var ix = blockX + MmToPx(part.Bounds.X, dpi);
        var iy = blockY + MmToPx(part.Bounds.Y, dpi);
        var iw = MmToPx(Math.Max(0.5, part.Bounds.Width), dpi);
        var ih = MmToPx(Math.Max(0.5, part.Bounds.Height), dpi);
        canvas.DrawBitmap(icon, new SKRect(ix, iy, ix + iw, iy + ih));
    }

    private static void DrawAddonsKitchenText(
        SKCanvas canvas,
        AddonsKitchenPart part,
        TemplateElementDocument element,
        float blockX,
        float blockY,
        int dpi)
    {
        var fontModel = part.Font ?? element.Font ?? new TemplateFont { Family = "Inter", SizePt = 8, Bold = true };
        var sizePt = Math.Max(4, fontModel.SizePt);
        var typeface = LabelAssets.ResolveTypeface(fontModel.Family, fontModel.Bold);
        var sizePx = (float)(sizePt * dpi / 72d);
        using var skFont = new SKFont(typeface, sizePx);
        using var paint = new SKPaint
        {
            Color = (part.Invert || element.Invert) ? SKColors.White : SKColors.Black,
            IsAntialias = true
        };

        var text = part.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var boxX = blockX + MmToPx(part.Bounds.X, dpi);
        var boxY = blockY + MmToPx(part.Bounds.Y, dpi);
        var boxW = MmToPx(Math.Max(1, part.Bounds.Width), dpi);
        var boxH = MmToPx(Math.Max(sizePt * 25.4 / 72.0, part.Bounds.Height), dpi);
        var skAlign = fontModel.HorizontalAlign switch
        {
            TextHorizontalAlign.Center => SKTextAlign.Center,
            TextHorizontalAlign.Right => SKTextAlign.Right,
            _ => SKTextAlign.Left
        };
        var tx = skAlign switch
        {
            SKTextAlign.Center => boxX + boxW / 2f,
            SKTextAlign.Right => boxX + boxW,
            _ => boxX
        };
        var baseline = fontModel.VerticalAlign switch
        {
            TextVerticalAlign.Middle => boxY + (boxH + sizePx) / 2f,
            TextVerticalAlign.Bottom => boxY + boxH,
            _ => boxY + sizePx
        };
        canvas.DrawText(text, tx, baseline, skAlign, skFont, paint);
    }

    private static void DrawAddonsKitchenRowText(
        SKCanvas canvas,
        AddonsKitchenPart part,
        TemplateElementDocument element,
        string text,
        float blockX,
        float rowOriginY,
        int dpi)
    {
        var fontModel = part.Font ?? element.Font ?? new TemplateFont { Family = "Inter", SizePt = 8, Bold = true };
        var sizePt = Math.Max(4, fontModel.SizePt);
        var typeface = LabelAssets.ResolveTypeface(fontModel.Family, fontModel.Bold);
        var sizePx = (float)(sizePt * dpi / 72d);
        using var skFont = new SKFont(typeface, sizePx);
        using var paint = new SKPaint
        {
            Color = (part.Invert || element.Invert) ? SKColors.White : SKColors.Black,
            IsAntialias = true
        };

        var boxX = blockX + MmToPx(part.Bounds.X, dpi);
        var boxY = rowOriginY + MmToPx(part.Bounds.Y, dpi);
        var boxW = MmToPx(Math.Max(1, part.Bounds.Width), dpi);
        var boxH = MmToPx(Math.Max(sizePt * 25.4 / 72.0, part.Bounds.Height), dpi);
        var lines = WrapText(text, skFont, boxW).Take(AddonsKitchenLayoutDefaults.MaxLinesPerItem).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        var lineStep = sizePx * 1.1f;
        var blockHeight = sizePx + (lines.Count - 1) * lineStep;
        var startBaseline = fontModel.VerticalAlign switch
        {
            TextVerticalAlign.Middle => boxY + Math.Max(0f, (boxH - blockHeight) / 2f) + sizePx,
            TextVerticalAlign.Bottom => boxY + Math.Max(0f, boxH - blockHeight) + sizePx,
            _ => boxY + sizePx
        };
        var skAlign = fontModel.HorizontalAlign switch
        {
            TextHorizontalAlign.Center => SKTextAlign.Center,
            TextHorizontalAlign.Right => SKTextAlign.Right,
            _ => SKTextAlign.Left
        };
        var tx = skAlign switch
        {
            SKTextAlign.Center => boxX + boxW / 2f,
            SKTextAlign.Right => boxX + boxW,
            _ => boxX
        };

        var textY = startBaseline;
        foreach (var line in lines)
        {
            canvas.DrawText(line, tx, textY, skAlign, skFont, paint);
            textY += lineStep;
        }
    }

    private static void DrawAddonsKitchenLine(
        SKCanvas canvas,
        AddonsKitchenPart part,
        TemplateElementDocument element,
        float blockX,
        float originY,
        int dpi)
    {
        var color = (part.Invert || element.Invert) ? SKColors.White : SKColors.Black;
        var stroke = Math.Max(1f, MmToPx(Math.Max(0.1, part.StrokeThickness), dpi));
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            StrokeWidth = stroke,
            Style = SKPaintStyle.Stroke,
            PathEffect = part.Dashed ? SKPathEffect.CreateDash([5f, 4f], 0f) : null
        };

        var x1 = blockX + MmToPx(part.Bounds.X, dpi);
        var y1 = originY + MmToPx(part.Bounds.Y, dpi) + stroke * 0.5f;
        var x2 = x1 + MmToPx(Math.Max(0.5, part.Bounds.Width), dpi);
        canvas.DrawLine(x1, y1, x2, y1, paint);
    }

    private static void DrawImage(
        SKCanvas canvas,
        TemplateElementDocument element,
        IReadOnlyDictionary<string, string> variables,
        float x,
        float y,
        float width,
        float height)
    {
        var path = ResolveImageSource(element, variables);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        using var bitmap = LabelAssets.TryLoadIcon(path);
        if (bitmap is null)
        {
            if (File.Exists(path))
            {
                using var fileBmp = SKBitmap.Decode(path);
                if (fileBmp is not null)
                {
                    using var tintedFile = TintIcon(fileBmp, element.Invert);
                    canvas.DrawBitmap(tintedFile, new SKRect(x, y, x + width, y + height));
                }
            }

            return;
        }

        using var tinted = TintIcon(bitmap, element.Invert);
        canvas.DrawBitmap(tinted, new SKRect(x, y, x + width, y + height));
    }

    /// <summary>
    /// Force opaque icon pixels to black or white (thermal mono), keeping alpha.
    /// </summary>
    private static SKBitmap TintIcon(SKBitmap source, bool white)
    {
        var copy = source.Copy()
                   ?? new SKBitmap(source.Info);
        if (!ReferenceEquals(copy, source))
        {
            source.CopyTo(copy);
        }

        var target = white ? (byte)255 : (byte)0;
        for (var y = 0; y < copy.Height; y++)
        {
            for (var x = 0; x < copy.Width; x++)
            {
                var c = copy.GetPixel(x, y);
                if (c.Alpha == 0)
                {
                    continue;
                }

                copy.SetPixel(x, y, new SKColor(target, target, target, c.Alpha));
            }
        }

        return copy;
    }

    private static string? ResolveImageSource(
        TemplateElementDocument element,
        IReadOnlyDictionary<string, string> variables)
    {
        if (element.BindingMode == TextBindingMode.Variable && !string.IsNullOrWhiteSpace(element.ValueBinding))
        {
            var fromVariable = LookupVariable(variables, element.ValueBinding);
            if (!string.IsNullOrWhiteSpace(fromVariable))
            {
                return fromVariable;
            }

            // Variable-bound icon with empty key → draw nothing (no fallback to static imagePath).
            return null;
        }

        return element.ImagePath;
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
        var maxLines = Math.Max(1, (int)Math.Floor(height / Math.Max(1f, lineHeight)));
        // Single-line boxes must not word-wrap on spaces ("03.08.2026 14:30" → time was dropped).
        var lines = maxLines <= 1
            ? text.Replace("\r\n", "\n").Split('\n').Take(1).ToList()
            : WrapText(text, skFont, Math.Max(1f, width));
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

        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + width, y + height));
        foreach (var line in lines)
        {
            canvas.DrawText(line, drawX, drawY, skAlign, skFont, paint);
            drawY += lineHeight;
            if (drawY - y > height + textSize * 0.25f)
            {
                break;
            }
        }

        canvas.Restore();
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

        var radius = MmToPx(element.CornerRadiusMm, dpi, minPixels: 0);
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

    private static int MmToPx(double mm, int dpi, int minPixels = 1) =>
        Math.Max(minPixels, (int)Math.Round(mm * dpi / 25.4d, MidpointRounding.AwayFromZero));
}
