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
}
