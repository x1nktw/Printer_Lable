using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Configured printer device.
/// </summary>
public class Printer : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public PrinterProtocol Protocol { get; set; } = PrinterProtocol.Windows;

    /// <summary>Windows queue name, COM port, IP:port, or output folder for File protocol.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public double PaperWidthMm { get; set; } = 58;

    /// <summary>
    /// Force 90° clockwise rotation when sending to Windows/TSPL.
    /// When false, Windows gateway may still auto-rotate portrait designs onto wider rolls.
    /// </summary>
    public bool Rotate90 { get; set; }

    /// <summary>
    /// Extra horizontal shift for Windows GDI print (mm). Positive moves content right.
    /// Applied after hard-margin compensation.
    /// </summary>
    public double PrintOffsetXMm { get; set; }

    /// <summary>
    /// Extra vertical shift for Windows GDI print (mm). Positive moves content down.
    /// If the top is clipped and the bottom is empty, try a negative value (e.g. -2).
    /// </summary>
    public double PrintOffsetYMm { get; set; }

    public int Dpi { get; set; } = 203;

    /// <summary>Print darkness / density (device-specific, typically 0-15).</summary>
    public int Darkness { get; set; } = 8;

    /// <summary>Print speed (device-specific).</summary>
    public int Speed { get; set; } = 4;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public ICollection<LabelTemplate> Templates { get; set; } = new List<LabelTemplate>();

    public ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();
}
