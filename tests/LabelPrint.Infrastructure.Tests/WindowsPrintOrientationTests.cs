using FluentAssertions;
using LabelPrint.Domain.Entities;
using LabelPrint.Infrastructure.Printing.Gateways;

namespace LabelPrint.Infrastructure.Tests;

public class WindowsPrintOrientationTests
{
    [Fact]
    public void Portrait_design_on_58mm_roll_rotates()
    {
        var printer = new Printer { PaperWidthMm = 58, Rotate90 = false };
        var (w, h, rotate) = WindowsPrintApiGateway.ResolveOrientation(printer, designW: 40, designH: 58);
        rotate.Should().BeTrue();
        w.Should().Be(58);
        h.Should().Be(40);
    }

    [Fact]
    public void Portrait_design_on_40mm_roll_stays()
    {
        var printer = new Printer { PaperWidthMm = 40, Rotate90 = false };
        var (w, h, rotate) = WindowsPrintApiGateway.ResolveOrientation(printer, designW: 40, designH: 58);
        rotate.Should().BeFalse();
        w.Should().Be(40);
        h.Should().Be(58);
    }

    [Fact]
    public void Force_rotate_overrides_roll_width()
    {
        var printer = new Printer { PaperWidthMm = 40, Rotate90 = true };
        var (w, h, rotate) = WindowsPrintApiGateway.ResolveOrientation(printer, designW: 40, designH: 58);
        rotate.Should().BeTrue();
        w.Should().Be(58);
        h.Should().Be(40);
    }

    [Fact]
    public void Print_origin_pads_when_driver_reports_zero_hard_margin()
    {
        var (x, y) = WindowsPrintApiGateway.ResolvePrintOrigin(
            hardMarginXHi: 0,
            hardMarginYHi: 0,
            printableAreaXHi: 0,
            printableAreaYHi: 0,
            offsetXMm: 0,
            offsetYMm: 0);

        // ~1.5 mm upward pad in hundredths of an inch
        y.Should().BeApproximately(-100f / 25.4f * 1.5f, 0.05f);
        x.Should().BeApproximately(-100f / 25.4f * 0.75f, 0.05f);
    }

    [Fact]
    public void Print_origin_applies_user_offset_mm()
    {
        var (x, y) = WindowsPrintApiGateway.ResolvePrintOrigin(
            hardMarginXHi: 0,
            hardMarginYHi: 10,
            printableAreaXHi: 0,
            printableAreaYHi: 10,
            offsetXMm: 1,
            offsetYMm: -2);

        x.Should().BeApproximately(-100f / 25.4f * 0.75f + 100f / 25.4f * 1f, 0.1f);
        // insetY = 10 hi + 0.75 mm pad; then + (-2 mm) user offset
        var expectedY = -(10f + 100f / 25.4f * 0.75f) + 100f / 25.4f * -2f;
        y.Should().BeApproximately(expectedY, 0.1f);
    }

    [Fact]
    public void Print_origin_prefers_larger_of_hard_margin_and_printable_area()
    {
        var (x, y) = WindowsPrintApiGateway.ResolvePrintOrigin(
            hardMarginXHi: 5,
            hardMarginYHi: 5,
            printableAreaXHi: 12,
            printableAreaYHi: 20,
            offsetXMm: 0,
            offsetYMm: 0);

        // X uses printable 12 (> pad), Y uses 20 + half pad
        x.Should().Be(-12f);
        y.Should().BeApproximately(-(20f + 100f / 25.4f * 0.75f), 0.1f);
    }
}
