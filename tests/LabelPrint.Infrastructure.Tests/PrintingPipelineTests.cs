using FluentAssertions;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;
using LabelPrint.Infrastructure.Printing.Gateways;
using LabelPrint.Infrastructure.Printing.Options;
using LabelPrint.Infrastructure.Printing.Rendering;
using LabelPrint.Plugins.Abstractions.Printing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelPrint.Infrastructure.Tests;

public class PrintingPipelineTests
{
    [Fact]
    public async Task SkiaLabelRenderService_ProducesNonEmptyPng()
    {
        var service = new SkiaLabelRenderService();
        var document = new TemplateDocument
        {
            Canvas = new TemplateCanvas { WidthMm = 58, HeightMm = 40, Dpi = 203 },
            Elements =
            [
                new TemplateElementDocument
                {
                    Type = TemplateElementType.Text,
                    Content = "Test {{ProductName}}",
                    Bounds = new TemplateBounds { X = 2, Y = 2, Width = 50, Height = 10 },
                    Font = new TemplateFont { Family = "Arial", SizePt = 12, Bold = true }
                },
                new TemplateElementDocument
                {
                    Type = TemplateElementType.Barcode,
                    BindingMode = TextBindingMode.Variable,
                    ValueBinding = "Barcode",
                    Symbology = BarcodeSymbology.Code128,
                    Bounds = new TemplateBounds { X = 2, Y = 14, Width = 40, Height = 12 }
                }
            ]
        };

        var variables = new Dictionary<string, string>
        {
            ["ProductName"] = "Milk",
            ["Barcode"] = "4601234567890"
        };

        RenderedLabel rendered = await service.RenderAsync(document, variables);

        rendered.Payload.Should().NotBeNullOrEmpty();
        rendered.ContentType.Should().Be("image/png");
        rendered.Payload.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task FilePrinterGateway_WritesPngToConfiguredDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LabelPrintProTests", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new PrintingOptions { OutputDirectory = tempDir });
        var gateway = new FilePrinterGateway(options, NullLogger<FilePrinterGateway>.Instance);

        var printer = new Domain.Entities.Printer
        {
            Name = "Virtual",
            Protocol = PrinterProtocol.File,
            ConnectionString = tempDir,
            Dpi = 203
        };

        var label = new RenderedLabel([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png", 58, 40, 203);

        await gateway.PrintAsync(printer, label, copies: 1);

        var files = Directory.GetFiles(tempDir, "label_*.png");
        files.Should().HaveCount(1);
        File.ReadAllBytes(files[0]).Should().StartWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup on CI/agents.
        }
    }
}
