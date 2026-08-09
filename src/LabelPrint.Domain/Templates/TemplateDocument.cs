using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.Templates;

/// <summary>
/// Versioned label template document stored in <c>LabelTemplate.ContentJson</c>.
/// </summary>
public sealed class TemplateDocument
{
    public int SchemaVersion { get; set; } = 1;

    public string? TemplateId { get; set; }

    public string? Name { get; set; }

    public TemplateCanvas Canvas { get; set; } = new();

    public List<TemplateElementDocument> Elements { get; set; } = new();
}

/// <summary>Canvas size and DPI.</summary>
public sealed class TemplateCanvas
{
    public double WidthMm { get; set; } = 58;

    public double HeightMm { get; set; } = 40;

    public int Dpi { get; set; } = 203;
}

/// <summary>Single element inside a template document.</summary>
public sealed class TemplateElementDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public TemplateElementType Type { get; set; }

    public TemplateBounds Bounds { get; set; } = new();

    public double Rotation { get; set; }

    public int Z { get; set; }

    public bool IsLocked { get; set; }

    public bool IsVisible { get; set; } = true;

    public string? GroupId { get; set; }

    public string? Name { get; set; }

    /// <summary>Literal text or variable placeholder for Text elements.</summary>
    public string? Content { get; set; }

    public TextBindingMode BindingMode { get; set; } = TextBindingMode.Literal;

    /// <summary>Variable key without braces, e.g. ProductName or Custom.Field.</summary>
    public string? ValueBinding { get; set; }

    public TemplateFont? Font { get; set; }

    public BarcodeSymbology? Symbology { get; set; }

    public string? ImagePath { get; set; }

    public double StrokeThickness { get; set; } = 0.3;

    public bool Filled { get; set; }

    /// <summary>Draw with inverted colors (white on black) for thermal header/badge text.</summary>
    public bool Invert { get; set; }

    /// <summary>Dashed stroke for lines.</summary>
    public bool Dashed { get; set; }

    /// <summary>Corner radius in mm for rectangles (0 = sharp).</summary>
    public double CornerRadiusMm { get; set; }

    /// <summary>
    /// Inner layout for <c>AddonsKitchen</c> variable blocks. Null = renderer defaults.
    /// </summary>
    public AddonsKitchenLayout? AddonsKitchen { get; set; }
}

/// <summary>Editable layout for the AddonsKitchen composite block.</summary>
public sealed class AddonsKitchenLayout
{
    public AddonsKitchenPart Title { get; set; } = new();

    public AddonsKitchenPart? Underline { get; set; }

    /// <summary>Y origin of the first addon row, relative to the block top (mm).</summary>
    public double RowsOriginYMm { get; set; }

    /// <summary>Height of one addon row (mm).</summary>
    public double RowHeightMm { get; set; } = 3.5;

    /// <summary>Gap after each row / separator before the next row (mm).</summary>
    public double RowGapMm { get; set; }

    /// <summary>Icon slot within a row (bounds relative to row origin).</summary>
    public AddonsKitchenPart Icon { get; set; } = new();

    /// <summary>Text slot within a row (bounds relative to row origin).</summary>
    public AddonsKitchenPart Text { get; set; } = new();

    /// <summary>Dashed separator under a row (bounds relative to row origin).</summary>
    public AddonsKitchenPart? Separator { get; set; }

    /// <summary>
    /// Legacy single empty-state text. Prefer <see cref="EmptyElements"/>.
    /// </summary>
    public AddonsKitchenPart? Empty { get; set; }

    /// <summary>
    /// Freeform text/image elements drawn when the addon list is empty.
    /// </summary>
    public List<AddonsKitchenPart> EmptyElements { get; set; } = new();
}

/// <summary>One editable part of an AddonsKitchen layout.</summary>
public sealed class AddonsKitchenPart
{
    public bool Visible { get; set; } = true;

    /// <summary><c>text</c> (default) or <c>image</c> — used for empty-state freeform items.</summary>
    public string PartType { get; set; } = "text";

    /// <summary>Optional label / text content.</summary>
    public string? Content { get; set; }

    /// <summary>Icon key or path for image parts.</summary>
    public string? ImagePath { get; set; }

    public TemplateBounds Bounds { get; set; } = new();

    public TemplateFont? Font { get; set; }

    public bool Invert { get; set; }

    public double StrokeThickness { get; set; } = 0.3;

    public bool Dashed { get; set; }
}

/// <summary>Element bounds in millimeters.</summary>
public sealed class TemplateBounds
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }
}

/// <summary>Font settings for text elements.</summary>
public sealed class TemplateFont
{
    public string Family { get; set; } = "Arial";

    public double SizePt { get; set; } = 10;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public TextHorizontalAlign HorizontalAlign { get; set; } = TextHorizontalAlign.Left;

    public TextVerticalAlign VerticalAlign { get; set; } = TextVerticalAlign.Top;
}
