using Avalonia.Media;

namespace LabelPrint.UI;

/// <summary>Lucide/Heroicons-style outline glyphs for chrome UI (24×24 viewBox).</summary>
public static class AppIcons
{
    public static Geometry Home { get; } = Parse(
        "M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z");

    public static Geometry Catalog { get; } = Parse(
        "M4 4h7v7H4z M13 4h7v7h-7z M4 13h7v7H4z M13 13h7v7h-7z");

    public static Geometry Tag { get; } = Parse(
        "M12 3H5.5A1.5 1.5 0 0 0 4 4.5V12l8.5 8.5L21 12z M8 8h.01");

    public static Geometry Orders { get; } = Parse(
        "M8 6h13 M8 12h13 M8 18h13 M3.5 6h.01 M3.5 12h.01 M3.5 18h.01");

    // Sliders — clean stroke icon (gear paths render poorly at 18px)
    public static Geometry Settings { get; } = Parse(
        "M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6");

    public static Geometry PanelLeft { get; } = Parse(
        "M3 6h18 M3 12h18 M3 18h18");

    public static Geometry PanelLeftClose { get; } = Parse(
        "M3 6h18 M3 12h18 M3 18h18");

    public static Geometry LogOut { get; } = Parse(
        "M10 5H6a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h4 M14 16l4-4-4-4 M18 12H9");

    public static Geometry User { get; } = Parse(
        "M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4z M5 20a7 7 0 0 1 14 0");

    public static Geometry LogIn { get; } = Parse(
        "M14 5h4a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1h-4 M10 16l-4-4 4-4 M6 12h9");

    public static Geometry Save { get; } = Parse(
        "M5 4h11l3 3v13H5z M8 4v5h8 M8 20v-6h8v6");

    public static Geometry Refresh { get; } = Parse(
        "M4 12a8 8 0 0 1 13.5-5.8L20 4v6h-6 M20 12a8 8 0 0 1-13.5 5.8L4 20v-6h6");

    private static Geometry Parse(string data) => StreamGeometry.Parse(data);
}
