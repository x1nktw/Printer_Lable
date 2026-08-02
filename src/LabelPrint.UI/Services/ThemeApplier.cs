using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using LabelPrint.Domain.Enums;

namespace LabelPrint.UI.Services;

/// <summary>Applies light/dark/system theme and Fluent accent color.</summary>
public static class ThemeApplier
{
    public const string DefaultAccentHex = "#10A37F";
    private const string AccentBrushKey = "AppAccentBrush";

    public static void Apply(AppTheme theme, string? accentHex)
    {
        if (Avalonia.Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };

        ApplyAccent(accentHex);
    }

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
