using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Domain.Enums;
using LabelPrint.Plugins.Abstractions.Printing;

namespace LabelPrint.Infrastructure.Printing.Gateways;

/// <summary>
/// Selects a protocol-specific gateway based on the configured <see cref="Domain.Entities.Printer.Protocol"/>.
/// </summary>
public sealed class CompositePrinterGateway : IPrinterGateway
{
    private readonly IPrinterRepository _printers;
    private readonly IReadOnlyDictionary<PrinterProtocol, IProtocolPrinterGateway> _gateways;

    public CompositePrinterGateway(
        IPrinterRepository printers,
        IEnumerable<IProtocolPrinterGateway> gateways)
    {
        _printers = printers;
        _gateways = gateways.ToDictionary(g => g.Protocol);
    }

    /// <inheritdoc />
    public async Task<PrinterCapabilities> GetCapabilitiesAsync(Guid printerId, CancellationToken cancellationToken = default)
    {
        var printer = await RequirePrinterAsync(printerId, cancellationToken);
        return await ResolveGateway(printer).GetCapabilitiesAsync(printer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PrintAsync(Guid printerId, RenderedLabel label, int copies, CancellationToken cancellationToken = default)
    {
        var printer = await RequirePrinterAsync(printerId, cancellationToken);
        await ResolveGateway(printer).PrintAsync(printer, label, copies, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PrinterDeviceStatus> GetStatusAsync(Guid printerId, CancellationToken cancellationToken = default)
    {
        var printer = await RequirePrinterAsync(printerId, cancellationToken);
        return await ResolveGateway(printer).GetStatusAsync(printer, cancellationToken);
    }

    private IProtocolPrinterGateway ResolveGateway(Domain.Entities.Printer printer)
    {
        if (_gateways.TryGetValue(printer.Protocol, out var gateway))
        {
            return gateway;
        }

        throw new NotSupportedException($"Printer protocol '{printer.Protocol}' is not supported yet.");
    }

    private async Task<Domain.Entities.Printer> RequirePrinterAsync(Guid printerId, CancellationToken cancellationToken)
    {
        var printer = await _printers.GetByIdAsync(printerId, cancellationToken);
        if (printer is null)
        {
            throw new InvalidOperationException($"Printer '{printerId}' was not found.");
        }

        if (!printer.IsActive)
        {
            throw new InvalidOperationException($"Printer '{printer.Name}' is inactive.");
        }

        return printer;
    }
}
