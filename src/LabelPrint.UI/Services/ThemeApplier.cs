using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using LabelPrint.Domain.Enums;

namespace LabelPrint.UI.Services;

/// <summary>Applies built-in and custom UI themes plus Fluent accent color.</summary>
public static class ThemeApplier
{
    public const string DefaultAccentHex = "#10A37F";
    private const string AccentBrushKey = "AppAccentBrush";

    public static readonly ThemeVariant Medium = new("Medium", ThemeVariant.Dark);
    public static readonly ThemeVariant Blue = new("Blue", ThemeVariant.Dark);
    public static readonly ThemeVariant Forest = new("Forest", ThemeVariant.Dark);
    public static readonly ThemeVariant Violet = new("Violet", ThemeVariant.Dark);
    public static readonly ThemeVariant Ocean = new("Ocean", ThemeVariant.Dark);
    public static readonly ThemeVariant Warm = new("Warm", ThemeVariant.Dark);

    public static void Apply(AppTheme theme, string? accentHex)
    {
        if (Avalonia.Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = ToVariant(theme);
        ApplyAccent(accentHex);
    }

    public static ThemeVariant ToVariant(AppTheme theme) =>
        theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.System => ThemeVariant.Default,
            AppTheme.Medium => Medium,
            AppTheme.Blue => Blue,
            AppTheme.Forest => Forest,
            AppTheme.Violet => Violet,
            AppTheme.Ocean => Ocean,
            AppTheme.Warm => Warm,
            _ => ThemeVariant.Dark
        };

    public static bool IsLightChrome(AppTheme theme) =>
        theme is AppTheme.Light;

    public static void ApplyAccent(string? accentHex)
    {
        if (Avalonia.Application.Current is not { } app)
        {
            return;
        }

        if (!TryParseColor(accentHex, out var color))
        {
            color = Color.Parse(DefaultAccentHex);
        }

        try
        {
            var fluent = app.Styles.OfType<FluentTheme>().FirstOrDefault();
            if (fluent is not null)
            {
                SetPaletteAccent(fluent, ThemeVariant.Light, color);
                SetPaletteAccent(fluent, ThemeVariant.Dark, color);
            }

            SetAccentBrush(app, color);
        }
        catch
        {
            // Accent update is best-effort; never crash the UI.
        }
    }

    public static bool TryParseColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            color = Color.Parse(hex.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SetAccentBrush(Avalonia.Application app, Color color)
    {
        // Mutate in place so DynamicResource consumers (icons, buttons) update immediately.
        if (app.Resources.TryGetResource(AccentBrushKey, theme: null, out var existing)
            && existing is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        app.Resources[AccentBrushKey] = new SolidColorBrush(color);
    }

    private static void SetPaletteAccent(FluentTheme fluent, ThemeVariant variant, Color color)
    {
        if (fluent.Palettes.TryGetValue(variant, out var existing))
        {
            existing.Accent = color;
            return;
        }

        fluent.Palettes[variant] = new ColorPaletteResources { Accent = color };
    }
}
