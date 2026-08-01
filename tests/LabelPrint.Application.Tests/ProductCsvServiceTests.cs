using FluentAssertions;
using LabelPrint.Application.Services;
using LabelPrint.Application.Tests.Fakes;
using LabelPrint.Application.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelPrint.Application.Tests;

public class ProductCsvServiceTests
{
    [Fact]
    public async Task ImportAsync_Creates_Product_From_Csv_Row()
    {
        var uow = new InMemoryUnitOfWork();
        var productService = new ProductService(uow, new ProductUpsertDtoValidator(), NullLogger<ProductService>.Instance);
        var csvService = new ProductCsvService(uow, productService, NullLogger<ProductCsvService>.Instance);

        const string csv = """
            Name,Sku,Barcode,Price,CategoryName
            Test Product,CSV-001,4601234567890,199.50,
            """;

        var result = await csvService.ImportAsync(csv);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);

        var product = await uow.Products.GetBySkuAsync("CSV-001");
        product.Should().NotBeNull();
        product!.Name.Should().Be("Test Product");
        product.Barcode.Should().Be("4601234567890");
        product.PriceAmount.Should().Be(199.50m);
    }

    [Fact]
    public async Task ExportAsync_RoundTrips_Imported_Product()
    {
        var uow = new InMemoryUnitOfWork();
        var productService = new ProductService(uow, new ProductUpsertDtoValidator(), NullLogger<ProductService>.Instance);
        var csvService = new ProductCsvService(uow, productService, NullLogger<ProductCsvService>.Instance);

        const string csv = """
            Name,Sku,Barcode,Price,CategoryName
            "Coffee, dark",SKU-COMMA,123,10,
            """;

        var importResult = await csvService.ImportAsync(csv);
        importResult.IsSuccess.Should().BeTrue();

        var exportResult = await csvService.ExportAsync();
        exportResult.IsSuccess.Should().BeTrue();
        exportResult.Value.Should().Contain("Coffee, dark");
        exportResult.Value.Should().Contain("SKU-COMMA");
    }
}
