using LabelPrint.Domain.Templates;

namespace LabelPrint.Application.Templates;

/// <summary>
/// Default AddonsKitchen layout matching the historical hard-coded Skia drawer.
/// </summary>
public static class AddonsKitchenLayoutDefaults
{
    public const string DefaultTitle = "ДОБАВКИ:";
    public const string DefaultEmptyText = "Без добавок";
    public const string PartTypeText = "text";
    public const string PartTypeImage = "image";
    public const double IconSizeMm = 3.2;
    public const double IconTextGapMm = 1.2;
    public const double TitleUnderlineStrokeMm = 0.35;
    public const double SeparatorStrokeMm = 0.2;
    public const int MaxLinesPerItem = 2;

    public static AddonsKitchenLayout Create(TemplateFont? font, double blockWidthMm)
    {
        var family = string.IsNullOrWhiteSpace(font?.Family) ? "Inter" : font!.Family;
        var sizePt = font?.SizePt > 0 ? font.SizePt : 8;
        var bold = font?.Bold ?? true;

        var titlePt = Math.Max(8, sizePt);
        var rowPt = Math.Max(7, sizePt - 0.5);
        var titleH = PtToMm(titlePt);
        var rowTextH = PtToMm(rowPt);
        var afterTitleGap = titleH * 0.35;
        var afterLineGap = rowTextH * 0.55;
        var underlineY = titleH + afterTitleGap;
        var rowsOrigin = underlineY + TitleUnderlineStrokeMm + afterLineGap;
        var rowHeight = Math.Max(IconSizeMm + 0.25, rowTextH * 1.55);
        var rowGap = rowTextH * 0.35;
        var width = Math.Max(1, blockWidthMm);
        var emptyH = PtToMm(rowPt);

        return new AddonsKitchenLayout
        {
            Title = new AddonsKitchenPart
            {
                Visible = true,
                PartType = PartTypeText,
                Content = DefaultTitle,
                Bounds = new TemplateBounds { X = 0, Y = 0, Width = width, Height = titleH },
                Font = new TemplateFont { Family = family, SizePt = titlePt, Bold = bold },
                Invert = false
            },
            Underline = new AddonsKitchenPart
            {
                Visible = true,
                Bounds = new TemplateBounds { X = 0, Y = underlineY, Width = width, Height = TitleUnderlineStrokeMm },
                StrokeThickness = TitleUnderlineStrokeMm,
                Dashed = false
            },
            RowsOriginYMm = rowsOrigin,
            RowHeightMm = rowHeight,
            RowGapMm = rowGap,
            Icon = new AddonsKitchenPart
            {
                Visible = true,
                Bounds = new TemplateBounds { X = 0, Y = 0, Width = IconSizeMm, Height = IconSizeMm }
            },
            Text = new AddonsKitchenPart
            {
                Visible = true,
                PartType = PartTypeText,
                Bounds = new TemplateBounds
                {
                    X = IconSizeMm + IconTextGapMm,
                    Y = 0,
                    Width = Math.Max(1, width - IconSizeMm - IconTextGapMm),
                    Height = rowHeight
                },
                Font = new TemplateFont { Family = family, SizePt = rowPt, Bold = bold }
            },
            Separator = new AddonsKitchenPart
            {
                Visible = true,
                Bounds = new TemplateBounds { X = 0, Y = rowHeight, Width = width, Height = SeparatorStrokeMm },
                StrokeThickness = SeparatorStrokeMm,
                Dashed = true
            },
            EmptyElements =
            [
                new AddonsKitchenPart
                {
                    Visible = true,
                    PartType = PartTypeText,
                    Content = DefaultEmptyText,
                    Bounds = new TemplateBounds { X = 0, Y = 0, Width = width, Height = emptyH },
                    Font = new TemplateFont { Family = family, SizePt = rowPt, Bold = bold }
                }
            ]
        };
    }

    /// <summary>Returns a deep copy; fills missing parts from defaults.</summary>
    public static AddonsKitchenLayout Resolve(AddonsKitchenLayout? layout, TemplateFont? font, double blockWidthMm)
    {
        var defaults = Create(font, blockWidthMm);
        if (layout is null)
        {
            return defaults;
        }

        var emptyElements = NormalizeEmptyElements(layout, defaults);

        return new AddonsKitchenLayout
        {
            Title = ClonePart(layout.Title, defaults.Title),
            Underline = layout.Underline is null && defaults.Underline is null
                ? null
                : ClonePart(layout.Underline, defaults.Underline!),
            RowsOriginYMm = layout.RowsOriginYMm > 0 ? layout.RowsOriginYMm : defaults.RowsOriginYMm,
            RowHeightMm = layout.RowHeightMm > 0 ? layout.RowHeightMm : defaults.RowHeightMm,
            RowGapMm = layout.RowGapMm >= 0 ? layout.RowGapMm : defaults.RowGapMm,
            Icon = ClonePart(layout.Icon, defaults.Icon),
            Text = ClonePart(layout.Text, defaults.Text),
            Separator = layout.Separator is null && defaults.Separator is null
                ? null
                : ClonePart(layout.Separator, defaults.Separator!),
            Empty = null,
            EmptyElements = emptyElements
        };
    }

    public static AddonsKitchenLayout Clone(AddonsKitchenLayout layout) =>
        new()
        {
            Title = ClonePart(layout.Title, layout.Title),
            Underline = layout.Underline is null ? null : ClonePart(layout.Underline, layout.Underline),
            RowsOriginYMm = layout.RowsOriginYMm,
            RowHeightMm = layout.RowHeightMm,
            RowGapMm = layout.RowGapMm,
            Icon = ClonePart(layout.Icon, layout.Icon),
            Text = ClonePart(layout.Text, layout.Text),
            Separator = layout.Separator is null ? null : ClonePart(layout.Separator, layout.Separator),
            Empty = null,
            EmptyElements = NormalizeEmptyElements(layout, layout)
        };

    public static bool IsImagePart(AddonsKitchenPart part) =>
        string.Equals(part.PartType, PartTypeImage, StringComparison.OrdinalIgnoreCase);

    private static List<AddonsKitchenPart> NormalizeEmptyElements(
        AddonsKitchenLayout layout,
        AddonsKitchenLayout defaults)
    {
        if (layout.EmptyElements is { Count: > 0 })
        {
            return layout.EmptyElements.Select(p => ClonePart(p, p)).ToList();
        }

        // Migrate legacy single Empty field.
        if (layout.Empty is not null)
        {
            var migrated = ClonePart(layout.Empty, defaults.EmptyElements[0]);
            migrated.PartType = PartTypeText;
            return [migrated];
        }

        // Old templates with neither Empty nor EmptyElements: keep "draw nothing".
        if (!ReferenceEquals(layout, defaults))
        {
            return [];
        }

        return defaults.EmptyElements.Select(p => ClonePart(p, p)).ToList();
    }

    private static AddonsKitchenPart ClonePart(AddonsKitchenPart? source, AddonsKitchenPart fallback)
    {
        var s = source ?? fallback;
        var b = s.Bounds;
        return new AddonsKitchenPart
        {
            Visible = source?.Visible ?? fallback.Visible,
            PartType = string.IsNullOrWhiteSpace(s.PartType) ? fallback.PartType : s.PartType,
            Content = s.Content ?? fallback.Content,
            ImagePath = s.ImagePath ?? fallback.ImagePath,
            Bounds = new TemplateBounds
            {
                X = b.X,
                Y = b.Y,
                Width = b.Width > 0 ? b.Width : fallback.Bounds.Width,
                Height = b.Height > 0 ? b.Height : fallback.Bounds.Height
            },
            Font = CloneFont(s.Font) ?? CloneFont(fallback.Font),
            Invert = s.Invert,
            StrokeThickness = s.StrokeThickness > 0 ? s.StrokeThickness : fallback.StrokeThickness,
            Dashed = s.Dashed
        };
    }

    private static TemplateFont? CloneFont(TemplateFont? font) =>
        font is null
            ? null
            : new TemplateFont
            {
                Family = font.Family,
                SizePt = font.SizePt,
                Bold = font.Bold,
                Italic = font.Italic,
                HorizontalAlign = font.HorizontalAlign,
                VerticalAlign = font.VerticalAlign
            };

    private static double PtToMm(double pt) => pt * 25.4 / 72.0;
}
