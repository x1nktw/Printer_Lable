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
}
