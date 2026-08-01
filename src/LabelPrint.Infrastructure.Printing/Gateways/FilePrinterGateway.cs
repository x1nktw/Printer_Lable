using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Infrastructure.Printing.Options;
using LabelPrint.Plugins.Abstractions.Printing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelPrint.Infrastructure.Printing.Gateways;

/// <summary>
/// Virtual printer that writes rendered PNG labels to disk.
/// </summary>
internal sealed class FilePrinterGateway : IProtocolPrinterGateway
{
    private readonly IOptions<PrintingOptions> _options;
    private readonly ILogger<FilePrinterGateway> _logger;

    public FilePrinterGateway(IOptions<PrintingOptions> options, ILogger<FilePrinterGateway> logger)
    {
        _options = options;
        _logger = logger;
    }

    public PrinterProtocol Protocol => PrinterProtocol.File;

    public Task<PrinterCapabilities> GetCapabilitiesAsync(Printer printer, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrinterCapabilities(
            SupportsNativeBarcode: false,
            SupportsGapSensor: false,
            MaxDpi: printer.Dpi,
            SupportedMedia: ["PNG"]));

    public Task<PrinterDeviceStatus> GetStatusAsync(Printer printer, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrinterDeviceStatus(
            IsOnline: true,
            HasPaper: true,
            IsBusy: false,
            Message: "Virtual file printer is ready."));

    public Task PrintAsync(Printer printer, RenderedLabel label, int copies, CancellationToken cancellationToken = default)
    {
        var directory = PrintOutputHelper.ResolveDirectory(printer, _options);

        for (var copy = 1; copy <= copies; copy++)
        {
            var suffix = copies > 1 ? $"_{copy}" : string.Empty;
            var path = PrintOutputHelper.CreateTimestampedPath(directory, $"label{suffix}", ".png");
            File.WriteAllBytes(path, label.Payload);
            _logger.LogInformation("Saved label PNG to {Path}", path);
        }

        return Task.CompletedTask;
    }
}
