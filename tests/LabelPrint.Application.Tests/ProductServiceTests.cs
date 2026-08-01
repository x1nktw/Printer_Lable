using FluentAssertions;
using LabelPrint.Application.DTOs;
using LabelPrint.Application.Services;
using LabelPrint.Application.Tests.Fakes;
using LabelPrint.Application.Validation;
using LabelPrint.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelPrint.Application.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task CreateAsync_Succeeds_For_Valid_Product()
    {
        var uow = new InMemoryUnitOfWork();
        var service = CreateService(uow);

        var result = await service.CreateAsync(new ProductUpsertDto
        {
            Name = "Бургер",
            Sku = "BRG-001",
            PriceAmount = 350,
            Barcode = "4601234567890"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var stored = await uow.Products.GetByIdAsync(result.Value);
        stored.Should().NotBeNull();
        stored!.Sku.Should().Be("BRG-001");
    }

    [Fact]
    public async Task CreateAsync_Rejects_Duplicate_Sku()
    {
        var uow = new InMemoryUnitOfWork();
        var service = CreateService(uow);

        await service.CreateAsync(new ProductUpsertDto { Name = "A", Sku = "SKU-1", PriceAmount = 1 });
        var result = await service.CreateAsync(new ProductUpsertDto { Name = "B", Sku = "SKU-1", PriceAmount = 2 });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("SKU");
    }

    [Fact]
    public async Task CreateAsync_Requires_Custom_Field_When_Defined()
    {
        var uow = new InMemoryUnitOfWork();
        var fieldId = Guid.NewGuid();
        await uow.CustomFieldDefinitions.AddAsync(new CustomFieldDefinition
        {
            Id = fieldId,
            Name = "Состав",
            IsRequired = true
        });

        var service = CreateService(uow);
        var result = await service.CreateAsync(new ProductUpsertDto
        {
            Name = "Салат",
            Sku = "SLT-1",
            PriceAmount = 200
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Состав");
    }

    [Fact]
    public async Task ArchiveAsync_Hides_Product_From_Default_Search()
    {
        var uow = new InMemoryUnitOfWork();
        var service = CreateService(uow);
        var created = await service.CreateAsync(new ProductUpsertDto { Name = "Cola", Sku = "COLA", PriceAmount = 100 });

        await service.ArchiveAsync(created.Value);
        var search = await service.SearchAsync(null, null, includeArchived: false, skip: 0, take: 50);

        search.IsSuccess.Should().BeTrue();
        search.Value.TotalCount.Should().Be(0);
    }

    private static ProductService CreateService(InMemoryUnitOfWork uow) =>
        new(uow, new ProductUpsertDtoValidator(), NullLogger<ProductService>.Instance);
}
