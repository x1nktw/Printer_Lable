using FluentAssertions;
using LabelPrint.Application.Templates;
using LabelPrint.Infrastructure.Printing.Rendering;
using LabelPrint.Infrastructure.Persistence;

namespace LabelPrint.Infrastructure.Tests;

public class MarkingReferenceTemplateTests
{
    [Fact]
    public async Task Marking_58x40_Reference_Renders_Png()
    {
        // Same embedded JSON used by DatabaseInitializer system preset.
        var asm = typeof(DatabaseInitializer).Assembly;
        var resource = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("marking-58x40.json", StringComparison.OrdinalIgnoreCase));
        await using var stream = asm.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        var document = TemplateDocumentSerializer.Deserialize(json);
        document.Canvas.WidthMm.Should().Be(58);
        document.Canvas.HeightMm.Should().Be(40);
        document.Elements.Should().HaveCountGreaterThan(10);
        document.Elements.Should().Contain(e => e.ValueBinding == "Time");
        document.Elements.Should().Contain(e => e.ValueBinding == "ExpireTime");

        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductName"] = "СОУС ДЛЯ ШАУРМЫ",
            ["ProductIconKey"] = "sauce",
            ["Date"] = "03.08.2026",
            ["Time"] = "14:30",
            ["ExpireDate"] = "04.08.2026",
            ["ExpireTime"] = "14:30",
            ["TemperatureRegime"] = "+2…+6 °C"
        };

        var render = new SkiaLabelRenderService();
        var result = await render.RenderAsync(document, vars);
        result.Payload.Length.Should().BeGreaterThan(1500);
        result.Payload[0].Should().Be(0x89);

        var preview = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "exports",
            "marking-58x40-preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(preview)!);
        await File.WriteAllBytesAsync(preview, result.Payload);
    }

    [Fact]
    public async Task Single_Line_DateTime_Does_Not_Drop_Time()
    {
        // Regression: word-wrap + maxLines=1 dropped "14:30" after the space.
        var json =
            """
            {"schemaVersion":1,"name":"t","canvas":{"widthMm":58,"heightMm":40,"dpi":203},"elements":[
              {"id":"v","type":0,"bounds":{"x":2,"y":2,"width":22,"height":4.4},
               "content":"{{Date}} {{Time}}","bindingMode":0,
               "font":{"family":"Inter","sizePt":9,"bold":true,"horizontalAlign":"right","verticalAlign":"middle"}}
            ]}
            """;
        var document = TemplateDocumentSerializer.Deserialize(json);
        var withTime = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = "03.08.2026",
            ["Time"] = "14:30"
        };
        var dateOnly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = "03.08.2026",
            ["Time"] = ""
        };

        var render = new SkiaLabelRenderService();
        var a = await render.RenderAsync(document, withTime);
        var b = await render.RenderAsync(document, dateOnly);

        a.Payload.Should().NotEqual(b.Payload);
    }
}
