using System.Net.Sockets;
using System.Text;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Infrastructure.Printing.Options;
using LabelPrint.Plugins.Abstractions.Printing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace LabelPrint.Infrastructure.Printing.Gateways;

/// <summary>
/// CPCL gateway MVP: writes .cpcl command files (+ PNG) to disk.
/// When <see cref="Printer.ConnectionString"/> is host:port, sends raw CPCL over TCP after file export.
/// </summary>
internal sealed class CpclPrinterGateway : IProtocolPrinterGateway
{
    private readonly IOptions<PrintingOptions> _options;
    private readonly ILogger<CpclPrinterGateway> _logger;

    public CpclPrinterGateway(IOptions<PrintingOptions> options, ILogger<CpclPrinterGateway> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public PrinterProtocol Protocol => PrinterProtocol.Cpcl;

    /// <inheritdoc />
    public Task<PrinterCapabilities> GetCapabilitiesAsync(Printer printer, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrinterCapabilities(
            SupportsNativeBarcode: true,
            SupportsGapSensor: true,
            MaxDpi: printer.Dpi,
            SupportedMedia: ["CPCL", "PNG"]));

    /// <inheritdoc />
    public Task<PrinterDeviceStatus> GetStatusAsync(Printer printer, CancellationToken cancellationToken = default)
    {
        if (TryParseTcpEndpoint(printer.ConnectionString, out var host, out var port))
        {
            try
            {
                using var client = new TcpClient();
                client.ConnectAsync(host, port, cancellationToken).GetAwaiter().GetResult();
                return Task.FromResult(new PrinterDeviceStatus(true, true, false, $"CPCL host {host}:{port} reachable."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PrinterDeviceStatus(false, true, false, ex.Message));
            }
        }

        return Task.FromResult(new PrinterDeviceStatus(
            true,
            true,
            false,
            "CPCL file-export mode (commands written to output folder)."));
    }

    /// <inheritdoc />
    public async Task PrintAsync(Printer printer, RenderedLabel label, int copies, CancellationToken cancellationToken = default)
    {
        var directory = PrintOutputHelper.ResolveDirectory(printer, _options);
        var pngPath = PrintOutputHelper.CreateTimestampedPath(directory, "label", ".png");
        await File.WriteAllBytesAsync(pngPath, label.Payload, cancellationToken);

        var cpcl = BuildCpcl(printer, label, pngPath, copies);
        var cpclPath = Path.ChangeExtension(pngPath, ".cpcl");
        await File.WriteAllTextAsync(cpclPath, cpcl, Encoding.ASCII, cancellationToken);

        _logger.LogInformation("Wrote CPCL job to {CpclPath} (PNG: {PngPath})", cpclPath, pngPath);

        if (TryParseTcpEndpoint(printer.ConnectionString, out var host, out var port))
        {
            await SendRawAsync(host, port, cpcl, cancellationToken);
            _logger.LogInformation("Sent CPCL payload to {Host}:{Port}", host, port);
        }
    }

    internal static string BuildCpcl(Printer printer, RenderedLabel label, string pngPath, int copies)
    {
        var widthMm = label.WidthMm > 0 ? label.WidthMm : printer.PaperWidthMm;
        var heightMm = label.HeightMm > 0 ? label.HeightMm : 40;
        var dpi = label.Dpi > 0 ? label.Dpi : printer.Dpi;
        var widthDots = (int)Math.Round(widthMm / 25.4 * dpi);
        var heightDots = (int)Math.Round(heightMm / 25.4 * dpi);
        var qty = Math.Max(1, copies);

        var sb = new StringBuilder();
        sb.AppendLine($"! 0 200 200 {heightDots} {qty}");
        sb.AppendLine($"PAGE-WIDTH {widthDots}");
        sb.AppendLine("CLS");

        using var bitmap = SKBitmap.Decode(pngPath);
        if (bitmap is not null)
        {
            var pixels = ConvertToMonochrome(bitmap);
            var widthBytes = (pixels.Width + 7) / 8;
            sb.Append("EG ");
            sb.Append(widthBytes);
            sb.Append(' ');
            sb.Append(pixels.Height);
            sb.Append(" 0 0 ");
            sb.AppendLine(Convert.ToHexString(pixels.Data).ToLowerInvariant());
        }
        else
        {
            sb.AppendLine($"REM Could not decode PNG at {pngPath}; open PNG manually if needed.");
        }

        sb.AppendLine("PRINT");
        sb.AppendLine("REM MVP: CPCL exported to file. Raw TCP send occurs when ConnectionString is host:port.");
        return sb.ToString();
    }

    private static MonochromeData ConvertToMonochrome(SKBitmap source)
    {
        var width = source.Width;
        var height = source.Height;
        var widthBytes = (width + 7) / 8;
        var pixels = new byte[widthBytes * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = source.GetPixel(x, y);
                var luminance = (color.Red * 0.299) + (color.Green * 0.587) + (color.Blue * 0.114);
                if (luminance < 128)
                {
                    var byteIndex = (y * widthBytes) + (x / 8);
                    pixels[byteIndex] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        return new MonochromeData(width, height, pixels);
    }

    private static async Task SendRawAsync(string host, int port, string payload, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);
        await using var stream = client.GetStream();
        var bytes = Encoding.ASCII.GetBytes(payload);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static bool TryParseTcpEndpoint(string connectionString, out string host, out int port)
    {
        host = string.Empty;
        port = 9100;

        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains('\\') || connectionString.Contains('/'))
        {
            return false;
        }

        var parts = connectionString.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            host = parts[0];
            return !string.IsNullOrWhiteSpace(host);
        }

        if (parts.Length == 2 && int.TryParse(parts[1], out port))
        {
            host = parts[0];
            return !string.IsNullOrWhiteSpace(host);
        }

        return false;
    }

    private sealed record MonochromeData(int Width, int Height, byte[] Data);
}
