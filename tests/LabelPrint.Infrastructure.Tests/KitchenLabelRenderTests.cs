using FluentAssertions;
using LabelPrint.Application.Templates;
using LabelPrint.Infrastructure.Printing.Rendering;

namespace LabelPrint.Infrastructure.Tests;

public class KitchenLabelRenderTests
{
    [Fact]
    public async Task Kitchen_Check_40x58_Renders_Png()
    {
        var preset = await LoadKitchenPresetJsonAsync();
        var document = TemplateDocumentSerializer.Deserialize(preset);
        document.Canvas.WidthMm.Should().Be(40);
        document.Canvas.HeightMm.Should().Be(58);

        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderNumber"] = "65502",
            ["Date"] = "01.08.2026",
            ["Time"] = "15:32",
            ["PositionName"] = "Шаверма Сырная",
            ["Addons"] = "Добавить халапеньо\nДвойной сыр\nБез лука",
            ["AddonsKitchen"] = "Добавить халапеньо\nДвойной сыр\nБез лука",
            ["PositionIndex"] = "2",
            ["PositionTotal"] = "3"
        };

        var render = new SkiaLabelRenderService();
        var result = await render.RenderAsync(document, vars);
        result.Payload.Length.Should().BeGreaterThan(1000);
        result.WidthMm.Should().Be(40);
        result.HeightMm.Should().Be(58);

        // Smoke: PNG signature
        result.Payload[0].Should().Be(0x89);
        result.Payload[1].Should().Be((byte)'P');

        var preview = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "kitchen-check-40x58-preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(preview)!);
        await File.WriteAllBytesAsync(preview, result.Payload);
    }

    private static Task<string> LoadKitchenPresetJsonAsync()
    {
        return Task.FromResult("""
               {"schemaVersion":1,"name":"Кухня чек 40x58","canvas":{"widthMm":40,"heightMm":58,"dpi":203},"elements":[{"id":"hdr","type":4,"bounds":{"x":0,"y":0,"width":40,"height":12},"filled":true,"z":0},{"id":"lbl","type":0,"bounds":{"x":1.5,"y":0.7,"width":18,"height":3},"content":"Заказ:","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"num","type":0,"bounds":{"x":1.5,"y":3.4,"width":18,"height":8},"bindingMode":1,"valueBinding":"OrderNumber","invert":true,"font":{"family":"Inter","sizePt":16,"bold":true},"z":1},{"id":"vdiv","type":6,"bounds":{"x":20.5,"y":1.5,"width":0,"height":9},"invert":true,"dashed":true,"strokeThickness":0.22,"z":1},{"id":"ical","type":1,"bounds":{"x":22,"y":1.8,"width":3.2,"height":3.2},"imagePath":"asset:icons/calendar-white.png","z":1},{"id":"date","type":0,"bounds":{"x":25.8,"y":1.9,"width":13,"height":3.2},"bindingMode":1,"valueBinding":"Date","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"iclk","type":1,"bounds":{"x":22,"y":6.6,"width":3.2,"height":3.2},"imagePath":"asset:icons/clock-white.png","z":1},{"id":"time","type":0,"bounds":{"x":25.8,"y":6.7,"width":13,"height":3.2},"bindingMode":1,"valueBinding":"Time","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"hdiv","type":6,"bounds":{"x":0,"y":12,"width":40,"height":0},"dashed":true,"strokeThickness":0.28,"z":2},{"id":"name","type":0,"bounds":{"x":1.5,"y":13.2,"width":37,"height":13},"bindingMode":1,"valueBinding":"PositionName","font":{"family":"Inter","sizePt":14,"bold":true},"z":2},{"id":"addons","type":0,"bounds":{"x":1.5,"y":27,"width":37,"height":22},"bindingMode":1,"valueBinding":"AddonsKitchen","font":{"family":"Inter","sizePt":8,"bold":true},"z":2},{"id":"badge","type":4,"bounds":{"x":27.5,"y":51.5,"width":11,"height":5},"filled":true,"cornerRadiusMm":1.2,"z":3},{"id":"idx","type":0,"bounds":{"x":28,"y":52.1,"width":10,"height":4},"content":"{{PositionIndex}}/{{PositionTotal}}","invert":true,"font":{"family":"Inter","sizePt":9,"bold":true},"z":4}]}
               """);
    }
}
