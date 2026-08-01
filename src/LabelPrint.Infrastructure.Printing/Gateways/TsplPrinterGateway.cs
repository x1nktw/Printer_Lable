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
/// TSPL gateway MVP: writes .tspl command files (+ PNG) to disk.
/// When ConnectionString is host:port, sends raw TSPL over TCP after file export.
/// </summary>
internal sealed class TsplPrinterGateway : IProtocolPrinterGateway
{
    private readonly IOptions<PrintingOptions> _options;
    private readonly ILogger<TsplPrinterGateway> _logger;

    public TsplPrinterGateway(IOptions<PrintingOptions> options, ILogger<TsplPrinterGateway> logger)
    {
        _options = options;
        _logger = logger;
    }

    public PrinterProtocol Protocol => PrinterProtocol.Tspl;

    public Task<PrinterCapabilities> GetCapabilitiesAsync(Printer printer, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrinterCapabilities(
            SupportsNativeBarcode: true,
            SupportsGapSensor: true,
            MaxDpi: printer.Dpi,
            SupportedMedia: ["TSPL", "PNG"]));

    public Task<PrinterDeviceStatus> GetStatusAsync(Printer printer, CancellationToken cancellationToken = default)
    {
        if (TryParseTcpEndpoint(printer.ConnectionString, out var host, out var port))
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port, cancellationToken);
                connectTask.GetAwaiter().GetResult();
                return Task.FromResult(new PrinterDeviceStatus(true, true, false, $"TSPL host {host}:{port} reachable."));
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
            "TSPL file-export mode (commands written to output folder)."));
    }

    public async Task PrintAsync(Printer printer, RenderedLabel label, int copies, CancellationToken cancellationToken = default)
    {
        var directory = PrintOutputHelper.ResolveDirectory(printer, _options);
        var pngPath = PrintOutputHelper.CreateTimestampedPath(directory, "label", ".png");
        await File.WriteAllBytesAsync(pngPath, label.Payload, cancellationToken);

        var tspl = BuildTspl(printer, label, pngPath, copies);
        var tsplPath = Path.ChangeExtension(pngPath, ".tspl");
        await File.WriteAllTextAsync(tsplPath, tspl, Encoding.ASCII, cancellationToken);

        _logger.LogInformation("Wrote TSPL job to {TsplPath} (PNG: {PngPath})", tsplPath, pngPath);

        if (TryParseTcpEndpoint(printer.ConnectionString, out var host, out var port))
        {
            await SendRawAsync(host, port, tspl, cancellationToken);
            _logger.LogInformation("Sent TSPL payload to {Host}:{Port}", host, port);
        }
    }

    internal static string BuildTspl(Printer printer, RenderedLabel label, string pngPath, int copies)
    {
        var widthMm = label.WidthMm > 0 ? label.WidthMm : printer.PaperWidthMm;
        var heightMm = label.HeightMm > 0 ? label.HeightMm : 40;
        var sb = new StringBuilder();
        sb.AppendLine($"SIZE {widthMm:0.#} mm,{heightMm:0.#} mm");
        sb.AppendLine("GAP 2 mm,0 mm");
        sb.AppendLine("DIRECTION 1");
        sb.AppendLine($"DENSITY {Math.Clamp(printer.Darkness, 0, 15)}");
        sb.AppendLine($"SPEED {Math.Clamp(printer.Speed, 1, 10)}");
        sb.AppendLine("CLS");

        using var bitmap = SKBitmap.Decode(pngPath);
        if (bitmap is not null)
        {
            var mono = ConvertToMonochrome(bitmap);
            var widthBytes = (mono.Width + 7) / 8;
            sb.Append("BITMAP 0,0,");
            sb.Append(widthBytes);
            sb.Append(',');
            sb.Append(mono.Height);
            sb.Append(",0,");
            sb.AppendLine(Convert.ToHexString(mono.Pixels).ToLowerInvariant());
        }
        else
        {
            sb.AppendLine($"REM Could not decode PNG at {pngPath}; open PNG manually if needed.");
        }

        sb.AppendLine($"PRINT {Math.Max(1, copies)}");
        sb.AppendLine("REM MVP: TSPL exported to file. Raw TCP send occurs when ConnectionString is host:port.");
        return sb.ToString();
    }

    private static MonochromeBitmap ConvertToMonochrome(SKBitmap source)
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
                var isBlack = luminance < 128;

                if (isBlack)
                {
                    var byteIndex = (y * widthBytes) + (x / 8);
                    pixels[byteIndex] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        return new MonochromeBitmap(width, height, pixels);
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

    private sealed record MonochromeBitmap(int Width, int Height, byte[] Pixels);
}
