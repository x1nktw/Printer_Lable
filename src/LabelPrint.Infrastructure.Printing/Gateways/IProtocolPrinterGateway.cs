using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Plugins.Abstractions.Printing;

namespace LabelPrint.Infrastructure.Printing.Gateways;

/// <summary>
/// Protocol-specific printer adapter used by <see cref="CompositePrinterGateway"/>.
/// </summary>
public interface IProtocolPrinterGateway
{
    PrinterProtocol Protocol { get; }

    Task<PrinterCapabilities> GetCapabilitiesAsync(Printer printer, CancellationToken cancellationToken = default);

    Task PrintAsync(Printer printer, RenderedLabel label, int copies, CancellationToken cancellationToken = default);

    Task<PrinterDeviceStatus> GetStatusAsync(Printer printer, CancellationToken cancellationToken = default);
}
