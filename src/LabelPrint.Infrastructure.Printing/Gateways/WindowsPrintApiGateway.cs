using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Plugins.Abstractions.Printing;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Infrastructure.Printing.Gateways;

/// <summary>
/// Sends rendered PNG labels to a named Windows print queue via System.Drawing.Printing.
/// Draws at the label's physical millimetre size and rotates when the roll width
/// matches the template's long side (portrait design on landscape stock).
/// </summary>
internal sealed class WindowsPrintApiGateway : IProtocolPrinterGateway
{
    private readonly ILogger<WindowsPrintApiGateway> _logger;

    public WindowsPrintApiGateway(ILogger<WindowsPrintApiGateway> logger) => _logger = logger;

    public PrinterProtocol Protocol => PrinterProtocol.Windows;

    public Task<PrinterCapabilities> GetCapabilitiesAsync(Printer printer, CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ValidatePrinterName(printer);

        return Task.FromResult(new PrinterCapabilities(
            SupportsNativeBarcode: false,
            SupportsGapSensor: false,
            MaxDpi: printer.Dpi,
            SupportedMedia: ["PNG", "Raster"]));
    }

    public Task<PrinterDeviceStatus> GetStatusAsync(Printer printer, CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ValidatePrinterName(printer);

        var installed = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Any(name => name.Equals(printer.ConnectionString, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(new PrinterDeviceStatus(
            IsOnline: installed,
            HasPaper: installed,
            IsBusy: false,
            Message: installed ? null : $"Windows printer '{printer.ConnectionString}' was not found."));
    }

    public Task PrintAsync(Printer printer, RenderedLabel label, int copies, CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ValidatePrinterName(printer);

        if (!PrinterSettings.InstalledPrinters.Cast<string>()
                .Any(name => name.Equals(printer.ConnectionString, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Windows printer '{printer.ConnectionString}' is not installed. Check the queue name in printer settings.");
        }

        var designW = label.WidthMm > 0 ? label.WidthMm : 40;
        var designH = label.HeightMm > 0 ? label.HeightMm : 40;
        var (paperW, paperH, rotate90) = ResolveOrientation(printer, designW, designH);

        using var imageStream = new MemoryStream(label.Payload);
        using var sourceImage = Image.FromStream(imageStream);
        using var printImage = PreparePrintImage(sourceImage, rotate90);

        using var printDocument = new PrintDocument
        {
            PrinterSettings =
            {
                PrinterName = printer.ConnectionString,
                Copies = (short)Math.Clamp(copies, 1, short.MaxValue)
            }
        };

        printDocument.DefaultPageSettings.PaperSize = new PaperSize(
            "LabelPrint",
            MmToHundredthsInch(paperW),
            MmToHundredthsInch(paperH));
        printDocument.DefaultPageSettings.Landscape = false;
        printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        printDocument.OriginAtMargins = false;

        try
        {
            printDocument.DefaultPageSettings.PrinterResolution =
                new PrinterResolution
                {
                    Kind = PrinterResolutionKind.Custom,
                    X = printer.Dpi > 0 ? printer.Dpi : label.Dpi,
                    Y = printer.Dpi > 0 ? printer.Dpi : label.Dpi
                };
        }
        catch
        {
            // Some drivers reject custom resolution — ignore.
        }

        printDocument.PrintPage += (_, e) =>
        {
            if (e.Graphics is null)
            {
                throw new InvalidOperationException("Print graphics context is unavailable.");
            }

            // Stay in hundredths-of-an-inch (Display) so HardMargin / PageBounds share one unit system.
            e.Graphics.PageUnit = GraphicsUnit.Display;
            e.Graphics.PageScale = 1f;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = SmoothingMode.None;

            var (originX, originY) = ResolvePrintOrigin(
                e.PageSettings.HardMarginX,
                e.PageSettings.HardMarginY,
                e.PageSettings.PrintableArea.X,
                e.PageSettings.PrintableArea.Y,
                printer.PrintOffsetXMm,
                printer.PrintOffsetYMm);

            // Prefer the driver page size; fall back to the label paper we requested.
            var destW = e.PageBounds.Width > 0 ? e.PageBounds.Width : MmToHundredthsInch(paperW);
            var destH = e.PageBounds.Height > 0 ? e.PageBounds.Height : MmToHundredthsInch(paperH);

            e.Graphics.DrawImage(printImage, originX, originY, destW, destH);
            e.HasMorePages = false;

            _logger.LogInformation(
                "Windows print origin=({OriginX:0.##},{OriginY:0.##}) hi, page={PageW}x{PageH} hi, hard=({HardX:0.##},{HardY:0.##}), printable=({PrintX:0.##},{PrintY:0.##}), offsetMm=({OffX},{OffY})",
                originX,
                originY,
                destW,
                destH,
                e.PageSettings.HardMarginX,
                e.PageSettings.HardMarginY,
                e.PageSettings.PrintableArea.X,
                e.PageSettings.PrintableArea.Y,
                printer.PrintOffsetXMm,
                printer.PrintOffsetYMm);
        };

        try
        {
            printDocument.Print();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to print to Windows printer '{printer.ConnectionString}': {ex.Message}", ex);
        }

        _logger.LogInformation(
            "Sent label design {DesignW}×{DesignH} mm as page {PaperW}×{PaperH} mm (rotate90={Rotate}) to {PrinterName}",
            designW,
            designH,
            paperW,
            paperH,
            rotate90,
            printer.ConnectionString);

        return Task.CompletedTask;
    }

    private static Image PreparePrintImage(Image source, bool rotate90Clockwise)
    {
        if (!rotate90Clockwise)
        {
            return new Bitmap(source);
        }

        var rotated = new Bitmap(source);
        rotated.RotateFlip(RotateFlipType.Rotate90FlipNone);
        return rotated;
    }

    /// <summary>
    /// Decides whether to rotate so one physical sticker is used.
    /// Portrait template on a wider roll (PaperWidthMm ≈ design height) → rotate 90°.
    /// Explicit <see cref="Printer.Rotate90"/> overrides auto.
    /// </summary>
    internal static (double PaperW, double PaperH, bool Rotate90) ResolveOrientation(
        Printer printer,
        double designW,
        double designH)
    {
        if (printer.Rotate90)
        {
            return (designH, designW, true);
        }

        var roll = printer.PaperWidthMm > 0 ? printer.PaperWidthMm : designW;
        var matchShort = Math.Abs(roll - designW);
        var matchLong = Math.Abs(roll - designH);

        // e.g. design 40×58, roll width 58 → feed should be 40 mm (one label), rotate content.
        if (designH > designW + 0.5 && matchLong + 1.0 < matchShort)
        {
            return (designH, designW, true);
        }

        return (designW, designH, false);
    }

    /// <summary>
    /// Graphics origin is the printable area. Negative shift moves content toward the
    /// physical page edge so the header is not eaten by the top hard margin.
    /// Units: hundredths of an inch (GraphicsUnit.Display while printing).
    /// </summary>
    internal static (float OriginX, float OriginY) ResolvePrintOrigin(
        float hardMarginXHi,
        float hardMarginYHi,
        float printableAreaXHi,
        float printableAreaYHi,
        double offsetXMm,
        double offsetYMm)
    {
        // Some drivers zero HardMargin but fill PrintableArea (or the reverse).
        var insetX = Math.Max(0f, Math.Max(hardMarginXHi, printableAreaXHi));
        var insetY = Math.Max(0f, Math.Max(hardMarginYHi, printableAreaYHi));

        // Cheap thermal drivers often under-report the top unprintable strip by ~1–2 mm,
        // which leaves a clipped header and empty footer. Pad when the driver reports little.
        const float underReportPadHi = 100f / 25.4f * 1.5f; // 1.5 mm
        if (insetY < underReportPadHi)
        {
            insetY = underReportPadHi;
        }
        else
        {
            insetY += underReportPadHi * 0.5f; // ~0.75 mm extra when a margin is reported
        }

        if (insetX < underReportPadHi * 0.5f)
        {
            insetX = underReportPadHi * 0.5f;
        }

        return (
            -insetX + MmToHundredthsInchFloat(offsetXMm),
            -insetY + MmToHundredthsInchFloat(offsetYMm));
    }

    private static int MmToHundredthsInch(double mm) =>
        Math.Max(1, (int)Math.Round(mm / 25.4d * 100d, MidpointRounding.AwayFromZero));

    private static float MmToHundredthsInchFloat(double mm) =>
        (float)(mm / 25.4d * 100d);

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows print API is only available on Windows.");
        }
    }

    private static void ValidatePrinterName(Printer printer)
    {
        if (string.IsNullOrWhiteSpace(printer.ConnectionString))
        {
            throw new InvalidOperationException(
                "Windows printer requires a queue name in ConnectionString (Printer Settings → Connection).");
        }
    }
}
