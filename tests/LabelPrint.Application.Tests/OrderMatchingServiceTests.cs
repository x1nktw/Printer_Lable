using FluentAssertions;
using LabelPrint.Application.DTOs;
using LabelPrint.Application.Services;
using LabelPrint.Application.Tests.Fakes;
using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Tests;

public class OrderMatchingServiceTests
{
    [Fact]
    public async Task MatchAsync_Prefers_Sku_Over_Barcode_And_Name()
    {
        var uow = new InMemoryUnitOfWork();
        var skuProduct = new Product { Id = Guid.NewGuid(), Name = "By Sku", Sku = "SKU-1", Barcode = "111" };
        var barcodeProduct = new Product { Id = Guid.NewGuid(), Name = "By Barcode", Sku = "SKU-2", Barcode = "222" };
        await uow.Products.AddAsync(skuProduct);
        await uow.Products.AddAsync(barcodeProduct);

        var service = new OrderMatchingService(uow);
        var (product, status) = await service.MatchAsync("SKU-1", "222", "By Barcode");

        product!.Id.Should().Be(skuProduct.Id);
        status.Should().Be(OrderItemMatchStatus.MatchedBySku);
    }

    [Fact]
    public async Task MatchAsync_Falls_Back_To_Barcode()
    {
        var uow = new InMemoryUnitOfWork();
        var product = new Product { Id = Guid.NewGuid(), Name = "Cola", Sku = "DRK-1", Barcode = "4601234567890" };
        await uow.Products.AddAsync(product);

        var service = new OrderMatchingService(uow);
        var (matched, status) = await service.MatchAsync(null, "4601234567890", "Cola Zero");

        matched!.Id.Should().Be(product.Id);
        status.Should().Be(OrderItemMatchStatus.MatchedByBarcode);
    }

    [Fact]
    public async Task MatchAsync_Falls_Back_To_Name()
    {
        var uow = new InMemoryUnitOfWork();
        var product = new Product { Id = Guid.NewGuid(), Name = "Бургер", Sku = "BRG-1" };
        await uow.Products.AddAsync(product);

        var service = new OrderMatchingService(uow);
        var (matched, status) = await service.MatchAsync(null, null, "Бургер");

        matched!.Id.Should().Be(product.Id);
        status.Should().Be(OrderItemMatchStatus.MatchedByName);
    }

    [Fact]
    public async Task MatchAsync_Returns_Unmatched_Without_Creating_Product()
    {
        var uow = new InMemoryUnitOfWork();
        var service = new OrderMatchingService(uow);

        var (matched, status) = await service.MatchAsync("UNKNOWN", null, "Ghost Item");

        matched.Should().BeNull();
        status.Should().Be(OrderItemMatchStatus.Unmatched);
    }
}
