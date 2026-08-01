using SkiaSharp;

namespace LabelPrint.Infrastructure.Printing.Rendering;

/// <summary>
/// Embedded label fonts and monochrome icons shipped with the printing layer.
/// </summary>
internal static class LabelAssets
{
    private static readonly List<SKData> Pin = new();
    private static readonly Lazy<SKTypeface> InterRegular = new(() => LoadTypeface("Inter-Regular.ttf"));
    private static readonly Lazy<SKTypeface> InterBold = new(() => LoadTypeface("Inter-Bold.ttf"));
    private static readonly Dictionary<string, SKBitmap> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object IconGate = new();

    public static SKTypeface ResolveTypeface(string? family, bool bold)
    {
        if (string.IsNullOrWhiteSpace(family)
            || family.Equals("Inter", StringComparison.OrdinalIgnoreCase)
            || family.Equals("Arial", StringComparison.OrdinalIgnoreCase))
        {
            return bold ? InterBold.Value : InterRegular.Value;
        }

        return SKTypeface.FromFamilyName(family, bold ? SKFontStyle.Bold : SKFontStyle.Normal)
               ?? (bold ? InterBold.Value : InterRegular.Value);
    }

    public static SKBitmap? TryLoadIcon(string name)
    {
        var key = NormalizeIconKey(name);
        lock (IconGate)
        {
            if (IconCache.TryGetValue(key, out var cached))
            {
                return cached.Copy();
            }

            var asm = typeof(LabelAssets).Assembly;
            var resource = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($".{key}.png", StringComparison.OrdinalIgnoreCase)
                                     || n.EndsWith($"icons.{key}.png", StringComparison.OrdinalIgnoreCase));
            if (resource is null)
            {
                return null;
            }

            using var stream = asm.GetManifestResourceStream(resource);
            if (stream is null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bitmap = SKBitmap.Decode(ms.ToArray());
            if (bitmap is null)
            {
                return null;
            }

            IconCache[key] = bitmap;
            return bitmap.Copy();
        }
    }

    public static string ResolveAddonIconKey(string addonText)
    {
        var t = addonText.ToLowerInvariant();
        if (t.Contains("халапень") || t.Contains("перец") || t.Contains("chili") || t.Contains("jalap") || t.Contains("острый"))
        {
            return "pepper";
        }

        if (t.Contains("сыр") || t.Contains("cheese"))
        {
            return "cheese";
        }

        if (t.Contains("лук") || t.Contains("onion"))
        {
            return "onion";
        }

        return "bullet";
    }

    private static string NormalizeIconKey(string name)
    {
        var key = name.Trim().Replace('\\', '/');
        if (key.StartsWith("asset:", StringComparison.OrdinalIgnoreCase))
        {
            key = key["asset:".Length..];
        }

        if (key.StartsWith("icons/", StringComparison.OrdinalIgnoreCase))
        {
            key = key["icons/".Length..];
        }

        if (key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^4];
        }

        return key;
    }

    private static SKTypeface LoadTypeface(string fileName)
    {
        var asm = typeof(LabelAssets).Assembly;
        var resource = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resource is null)
        {
            return SKTypeface.Default;
        }

        using var stream = asm.GetManifestResourceStream(resource);
        if (stream is null)
        {
            return SKTypeface.Default;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = SKData.CreateCopy(ms.ToArray());
        Pin.Add(data); // keep native buffer alive for process lifetime
        return SKTypeface.FromData(data) ?? SKTypeface.Default;
    }
}
