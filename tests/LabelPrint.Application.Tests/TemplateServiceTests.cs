using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Services;
using LabelPrint.Application.Templates;
using LabelPrint.Application.Tests.Fakes;
using LabelPrint.Domain.Templates;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelPrint.Application.Tests;

public class TemplateServiceTests
{
    [Fact]
    public async Task Create_And_Save_Document_Roundtrips()
    {
        var uow = new InMemoryUnitOfWork();
        var service = new TemplateService(uow, NullLogger<TemplateService>.Instance);

        var created = await service.CreateAsync("Test 58x40", 58, 40);
        created.IsSuccess.Should().BeTrue();

        var doc = new TemplateDocument
        {
            SchemaVersion = 1,
            Name = "Test 58x40",
            Canvas = new TemplateCanvas { WidthMm = 58, HeightMm = 40, Dpi = 203 },
            Elements =
            {
                new TemplateElementDocument
                {
                    Type = Domain.Enums.TemplateElementType.Text,
                    Content = "{{ProductName}}",
                    BindingMode = Domain.Enums.TextBindingMode.Variable,
                    ValueBinding = "ProductName",
                    Bounds = new TemplateBounds { X = 2, Y = 2, Width = 50, Height = 10 }
                }
            }
        };

        var saved = await service.SaveDocumentAsync(created.Value, "Test 58x40", doc);
        saved.IsSuccess.Should().BeTrue();

        var loaded = await service.GetAsync(created.Value);
        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Document.Elements.Should().HaveCount(1);
        loaded.Value.Document.Elements[0].ValueBinding.Should().Be("ProductName");
    }

    [Fact]
    public void Serializer_Uses_CamelCase_Enums()
    {
        var json = TemplateDocumentSerializer.Serialize(new TemplateDocument
        {
            Elements =
            {
                new TemplateElementDocument { Type = Domain.Enums.TemplateElementType.Barcode, Symbology = Domain.Enums.BarcodeSymbology.Code128 }
            }
        });

        json.Should().Contain("barcode");
        json.Should().Contain("code128");
    }
}
