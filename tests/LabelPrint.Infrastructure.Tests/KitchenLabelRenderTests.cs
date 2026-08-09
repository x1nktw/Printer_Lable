using FluentAssertions;
using LabelPrint.Application.Templates;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;
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

    [Fact]
    public async Task AddonsKitchen_CustomLayout_Renders_Without_Title()
    {
        var font = new TemplateFont { Family = "Inter", SizePt = 8, Bold = true };
        var layout = AddonsKitchenLayoutDefaults.Create(font, 37);
        layout.Title.Visible = false;
        layout.Underline!.Visible = false;
        layout.Icon.Bounds = new TemplateBounds { X = 0, Y = 0, Width = 5, Height = 5 };
        layout.Text.Bounds = new TemplateBounds { X = 6.5, Y = 0, Width = 30, Height = 5 };
        layout.RowHeightMm = 5.5;
        layout.RowsOriginYMm = 1;

        var document = new TemplateDocument
        {
            SchemaVersion = 1,
            Name = "Addons layout",
            Canvas = new TemplateCanvas { WidthMm = 40, HeightMm = 30, Dpi = 203 },
            Elements =
            [
                new TemplateElementDocument
                {
                    Id = "addons",
                    Type = TemplateElementType.Text,
                    BindingMode = TextBindingMode.Variable,
                    ValueBinding = "AddonsKitchen",
                    Bounds = new TemplateBounds { X = 1.5, Y = 2, Width = 37, Height = 26 },
                    Font = font,
                    AddonsKitchen = layout
                }
            ]
        };

        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AddonsKitchen"] = "Добавить халапеньо\nДвойной сыр",
            ["Addons"] = "Добавить халапеньо\nДвойной сыр"
        };

        var render = new SkiaLabelRenderService();
        var custom = await render.RenderAsync(document, vars);
        custom.Payload.Length.Should().BeGreaterThan(500);

        var defaultsDoc = TemplateDocumentSerializer.Deserialize(
            TemplateDocumentSerializer.Serialize(document));
        defaultsDoc.Elements[0].AddonsKitchen = null;
        var withDefaults = await render.RenderAsync(defaultsDoc, vars);

        custom.Payload.Should().NotEqual(withDefaults.Payload);
    }

    [Fact]
    public async Task AddonsKitchen_EmptyState_Renders_When_No_Addons()
    {
        var font = new TemplateFont { Family = "Inter", SizePt = 8, Bold = true };
        var layout = AddonsKitchenLayoutDefaults.Create(font, 37);
        layout.EmptyElements =
        [
            new AddonsKitchenPart
            {
                Visible = true,
                PartType = "text",
                Content = "Нет добавок к позиции",
                Bounds = new TemplateBounds { X = 0, Y = 0, Width = 37, Height = 4 },
                Font = new TemplateFont { Family = "Inter", SizePt = 8, Bold = true }
            }
        ];

        var document = new TemplateDocument
        {
            SchemaVersion = 1,
            Canvas = new TemplateCanvas { WidthMm = 40, HeightMm = 20, Dpi = 203 },
            Elements =
            [
                new TemplateElementDocument
                {
                    Type = TemplateElementType.Text,
                    BindingMode = TextBindingMode.Variable,
                    ValueBinding = "AddonsKitchen",
                    Bounds = new TemplateBounds { X = 1, Y = 1, Width = 37, Height = 16 },
                    Font = font,
                    AddonsKitchen = layout
                }
            ]
        };

        var render = new SkiaLabelRenderService();
        var emptyVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AddonsKitchen"] = "",
            ["Addons"] = ""
        };
        var emptyResult = await render.RenderAsync(document, emptyVars);
        emptyResult.Payload.Length.Should().BeGreaterThan(500);

        var withAddons = await render.RenderAsync(document, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AddonsKitchen"] = "Двойной сыр",
            ["Addons"] = "Двойной сыр"
        });
        emptyResult.Payload.Should().NotEqual(withAddons.Payload);
    }

    private static Task<string> LoadKitchenPresetJsonAsync()
    {
        return Task.FromResult("""
               {"schemaVersion":1,"name":"Кухня чек 40x58","canvas":{"widthMm":40,"heightMm":58,"dpi":203},"elements":[{"id":"hdr","type":4,"bounds":{"x":0,"y":0,"width":40,"height":12},"filled":true,"z":0},{"id":"lbl","type":0,"bounds":{"x":1.5,"y":0.7,"width":18,"height":3},"content":"Заказ:","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"num","type":0,"bounds":{"x":1.5,"y":3.4,"width":18,"height":8},"bindingMode":1,"valueBinding":"OrderNumber","invert":true,"font":{"family":"Inter","sizePt":16,"bold":true},"z":1},{"id":"vdiv","type":6,"bounds":{"x":20.5,"y":1.5,"width":0,"height":9},"invert":true,"dashed":true,"strokeThickness":0.22,"z":1},{"id":"ical","type":1,"bounds":{"x":22,"y":1.8,"width":3.2,"height":3.2},"imagePath":"asset:icons/calendar-white.png","z":1},{"id":"date","type":0,"bounds":{"x":25.8,"y":1.9,"width":13,"height":3.2},"bindingMode":1,"valueBinding":"Date","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"iclk","type":1,"bounds":{"x":22,"y":6.6,"width":3.2,"height":3.2},"imagePath":"asset:icons/clock-white.png","z":1},{"id":"time","type":0,"bounds":{"x":25.8,"y":6.7,"width":13,"height":3.2},"bindingMode":1,"valueBinding":"Time","invert":true,"font":{"family":"Inter","sizePt":7},"z":1},{"id":"hdiv","type":6,"bounds":{"x":0,"y":12,"width":40,"height":0},"dashed":true,"strokeThickness":0.28,"z":2},{"id":"name","type":0,"bounds":{"x":1.5,"y":13.2,"width":37,"height":13},"bindingMode":1,"valueBinding":"PositionName","font":{"family":"Inter","sizePt":14,"bold":true},"z":2},{"id":"addons","type":0,"bounds":{"x":1.5,"y":27,"width":37,"height":22},"bindingMode":1,"valueBinding":"AddonsKitchen","font":{"family":"Inter","sizePt":8,"bold":true},"z":2},{"id":"badge","type":4,"bounds":{"x":27.5,"y":51.5,"width":11,"height":5},"filled":true,"cornerRadiusMm":1.2,"z":3},{"id":"idx","type":0,"bounds":{"x":28,"y":52.1,"width":10,"height":4},"content":"{{PositionIndex}}/{{PositionTotal}}","invert":true,"font":{"family":"Inter","sizePt":9,"bold":true},"z":4}]}
               """);
    }
}
