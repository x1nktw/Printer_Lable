using Avalonia.Media;

namespace LabelPrint.UI;

/// <summary>Lucide-style outline glyphs for chrome UI (24×24 viewBox, stroke-friendly).</summary>
public static class AppIcons
{
    public static Geometry Home { get; } = Parse(
        "M3 10a2 2 0 0 1 .709-1.528l7-6a2 2 0 0 1 2.582 0l7 6A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M9 22V12h6v10");

    public static Geometry Catalog { get; } = Parse(
        "M3 3h7v7H3z M14 3h7v7h-7z M14 14h7v7h-7z M3 14h7v7H3z");

    public static Geometry Tag { get; } = Parse(
        "M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.427 0l6.586-6.586a2.426 2.426 0 0 0 0-3.427z M7 7h.01");

    public static Geometry Orders { get; } = Parse(
        "M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01");

    public static Geometry Settings { get; } = Parse(
        "M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6");

    public static Geometry PanelLeft { get; } = Parse(
        "M3 6h18 M3 12h18 M3 18h18");

    public static Geometry PanelLeftClose { get; } = Parse(
        "M3 6h18 M3 12h18 M3 18h18");

    public static Geometry LogOut { get; } = Parse(
        "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4 M16 17l5-5-5-5 M21 12H9");

    public static Geometry User { get; } = Parse(
        "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2 M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z");

    public static Geometry LogIn { get; } = Parse(
        "M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4 M10 17l5-5-5-5 M15 12H3");

    public static Geometry Save { get; } = Parse(
        "M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M17 21v-7H7v7 M7 3v4h8");

    public static Geometry Refresh { get; } = Parse(
        "M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8 M21 3v5h-5 M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16 M8 16H3v5");

    /// <summary>Compact brand mark for titlebar/sidebar (full PNG logo is too detailed at 16–22px).</summary>
    public static Geometry Brand { get; } = Parse(
        "M7 4h10v3H7z M5 7h14v12a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1z M8 11h8 M8 14h5 M9 19h6");

    /// <summary>Word-style paragraph align left (uneven lines flush left).</summary>
    public static Geometry AlignLeft { get; } = Parse(
        "M3 6h18 M3 10h12 M3 14h18 M3 18h12");

    /// <summary>Word-style paragraph align center.</summary>
    public static Geometry AlignCenter { get; } = Parse(
        "M3 6h18 M6 10h12 M3 14h18 M6 18h12");

    /// <summary>Word-style paragraph align right.</summary>
    public static Geometry AlignRight { get; } = Parse(
        "M3 6h18 M9 10h12 M3 14h18 M9 18h12");

    /// <summary>Word-style vertical align top (lines near top of frame).</summary>
    public static Geometry AlignTop { get; } = Parse(
        "M5 3h14v18H5z M8 6h8 M8 10h8");

    /// <summary>Word-style vertical align middle.</summary>
    public static Geometry AlignMiddle { get; } = Parse(
        "M5 3h14v18H5z M8 9h8 M8 13h8");

    /// <summary>Word-style vertical align bottom.</summary>
    public static Geometry AlignBottom { get; } = Parse(
        "M5 3h14v18H5z M8 14h8 M8 18h8");

    private static Geometry Parse(string data) => StreamGeometry.Parse(data);
}
