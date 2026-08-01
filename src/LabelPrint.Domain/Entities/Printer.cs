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
