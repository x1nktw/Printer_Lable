namespace LabelPrint.Plugins.Abstractions.Printing;

/// <summary>
/// Port for sending rendered labels to a physical or virtual printer.
/// </summary>
public interface IPrinterGateway
{
    /// <summary>Returns capabilities of the configured printer.</summary>
    Task<PrinterCapabilities> GetCapabilitiesAsync(Guid printerId, CancellationToken cancellationToken = default);

    /// <summary>Sends a rendered label to the printer.</summary>
    Task PrintAsync(Guid printerId, RenderedLabel label, int copies, CancellationToken cancellationToken = default);

    /// <summary>Queries current printer device status.</summary>
    Task<PrinterDeviceStatus> GetStatusAsync(Guid printerId, CancellationToken cancellationToken = default);
}

/// <summary>Printer feature flags and limits.</summary>
public sealed record PrinterCapabilities(
    bool SupportsNativeBarcode,
    bool SupportsGapSensor,
    int MaxDpi,
    IReadOnlyList<string> SupportedMedia);

/// <summary>Rasterized or command-language ready label payload.</summary>
public sealed record RenderedLabel(
    byte[] Payload,
    string ContentType,
    double WidthMm,
    double HeightMm,
    int Dpi);

/// <summary>Live device status.</summary>
public sealed record PrinterDeviceStatus(
    bool IsOnline,
    bool HasPaper,
    bool IsBusy,
    string? Message);
