using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Plugins.Abstractions.Printing;

namespace LabelPrint.Infrastructure.Printing.Gateways;

/// <summary>
/// ESC/POS gateway placeholder. Registered so protocol selection fails clearly instead of falling through composite routing.
/// </summary>
internal sealed class EscPosPrinterGateway : IProtocolPrinterGateway
{
    private const string NotImplementedMessage = "ESC/POS not implemented yet";

    /// <inheritdoc />
    public PrinterProtocol Protocol => PrinterProtocol.EscPos;

    /// <inheritdoc />
    public Task<PrinterCapabilities> GetCapabilitiesAsync(Printer printer, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrinterCapabilities(
            SupportsNativeBarcode: false,
            SupportsGapSensor: false,
            MaxDpi: printer.Dpi,
            SupportedMedia: []));

    /// <inheritdoc />
    public Task<PrinterDeviceStatus> GetStatusAsync(Printer printer, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrinterDeviceStatus(false, true, false, NotImplementedMessage));

    /// <inheritdoc />
    public Task PrintAsync(Printer printer, RenderedLabel label, int copies, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NotImplementedMessage);
}
