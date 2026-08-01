using LabelPrint.Domain.Common;
using LabelPrint.Domain.ValueObjects;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Label template metadata; visual content stored as versioned JSON.
/// </summary>
public class LabelTemplate : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public double WidthMm { get; set; } = 58;

    public double HeightMm { get; set; } = 40;

    public int Dpi { get; set; } = 203;

    /// <summary>JSON schema version of <see cref="ContentJson"/>.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Versioned template document (elements, fonts, bindings).</summary>
    public string ContentJson { get; set; } = """{"schemaVersion":1,"elements":[]}""";

    public string? PreviewImagePath { get; set; }

    public Guid? DefaultPrinterId { get; set; }

    public Printer? DefaultPrinter { get; set; }

    public bool IsArchived { get; set; }

    public bool IsSystemPreset { get; set; }

    public LabelSize GetSize() => new(WidthMm, HeightMm);

    public void SetSize(LabelSize size)
    {
        WidthMm = size.WidthMm;
        HeightMm = size.HeightMm;
    }
}
