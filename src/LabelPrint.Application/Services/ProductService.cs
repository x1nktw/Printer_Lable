using FluentValidation;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Application service for product catalog operations.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<ProductUpsertDto> _validator;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IUnitOfWork unitOfWork,
        IValidator<ProductUpsertDto> validator,
        ILogger<ProductService> logger)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(ProductUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        if (await _unitOfWork.Products.SkuExistsAsync(dto.Sku, null, cancellationToken))
        {
            return Result.Failure<Guid>($"SKU '{dto.Sku}' already exists.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Barcode) &&
            await _unitOfWork.Products.BarcodeExistsAsync(dto.Barcode, null, cancellationToken))
        {
            return Result.Failure<Guid>($"Barcode '{dto.Barcode}' already exists.");
        }

        var categoryError = await ValidateCategoryAsync(dto.CategoryId, cancellationToken);
        if (categoryError is not null)
        {
            return Result.Failure<Guid>(categoryError);
        }

        var product = new Product();
        ApplyDto(product, dto);
        var customFieldsError = await ApplyCustomFieldsAsync(product, dto.CustomFieldValues, cancellationToken);
        if (customFieldsError is not null)
        {
            return Result.Failure<Guid>(customFieldsError);
        }

        await _unitOfWork.Products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Product {ProductId} created with SKU {Sku}", product.Id, product.Sku);
        return Result.Success(product.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, ProductUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null || product.IsArchived)
        {
            return Result.Failure("Product not found.");
        }

        if (await _unitOfWork.Products.SkuExistsAsync(dto.Sku, id, cancellationToken))
        {
            return Result.Failure($"SKU '{dto.Sku}' already exists.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Barcode) &&
            await _unitOfWork.Products.BarcodeExistsAsync(dto.Barcode, id, cancellationToken))
        {
            return Result.Failure($"Barcode '{dto.Barcode}' already exists.");
        }

        var categoryError = await ValidateCategoryAsync(dto.CategoryId, cancellationToken);
        if (categoryError is not null)
        {
            return Result.Failure(categoryError);
        }

        ApplyDto(product, dto);
        var customFieldsError = await ApplyCustomFieldsAsync(product, dto.CustomFieldValues, cancellationToken);
        if (customFieldsError is not null)
        {
            return Result.Failure(customFieldsError);
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Product {ProductId} updated", product.Id);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Failure("Product not found.");
        }

        await _unitOfWork.Products.SoftArchiveAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Product {ProductId} archived", id);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<ProductUpsertDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductUpsertDto>("Product not found.");
        }

        return Result.Success(MapToDto(product));
    }

    /// <inheritdoc />
    public async Task<Result<(IReadOnlyList<ProductListItemDto> Items, int TotalCount)>> SearchAsync(
        string? search,
        Guid? categoryId,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default,
        Guid? excludeCategoryId = null)
    {
        if (take <= 0 || take > 500)
        {
            return Result.Failure<(IReadOnlyList<ProductListItemDto>, int)>("Take must be between 1 and 500.");
        }

        if (skip < 0)
        {
            return Result.Failure<(IReadOnlyList<ProductListItemDto>, int)>("Skip cannot be negative.");
        }

        var (items, total) = await _unitOfWork.Products.SearchAsync(
            search, categoryId, includeArchived, skip, take, cancellationToken, excludeCategoryId);

        var dtos = items.Select(p => new ProductListItemDto
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            Barcode = p.Barcode,
            PriceAmount = p.PriceAmount,
            CategoryName = p.Category?.Name,
            IsArchived = p.IsArchived
        }).ToList();

        return Result.Success<(IReadOnlyList<ProductListItemDto>, int)>((dtos, total));
    }

    private async Task<string?> ValidateCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return null;
        }

        var category = await _unitOfWork.Categories.GetByIdAsync(categoryId.Value, cancellationToken);
        return category is null || category.IsArchived ? "Category not found." : null;
    }

    private async Task<string?> ApplyCustomFieldsAsync(
        Product product,
        Dictionary<Guid, string?> values,
        CancellationToken cancellationToken)
    {
        var definitions = await _unitOfWork.CustomFieldDefinitions.GetAllAsync(includeArchived: false, cancellationToken);
        var definitionMap = definitions.ToDictionary(d => d.Id);

        foreach (var required in definitions.Where(d => d.IsRequired))
        {
            if (!values.TryGetValue(required.Id, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return $"Custom field '{required.Name}' is required.";
            }
        }

        product.CustomFields.Clear();
        foreach (var (fieldId, value) in values)
        {
            if (!definitionMap.ContainsKey(fieldId))
            {
                continue;
            }

            product.CustomFields.Add(new ProductCustomField
            {
                ProductId = product.Id,
                FieldDefinitionId = fieldId,
                Value = value
            });
        }

        return null;
    }

    private static void ApplyDto(Product product, ProductUpsertDto dto)
    {
        product.Name = dto.Name.Trim();
        product.Sku = dto.Sku.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
        product.SetPrice(new Money(dto.PriceAmount, dto.PriceCurrency));
        product.SetWeight(dto.WeightValue is null ? null : new Weight(dto.WeightValue.Value, dto.WeightUnit));
        product.ManufactureDate = dto.ManufactureDate;
        product.ExpireDate = dto.ExpireDate;
        product.ShelfLifeDays = dto.ShelfLifeDays;
        product.CategoryId = dto.CategoryId;
        product.DefaultTemplateId = dto.DefaultTemplateId;
        product.OrderItemTemplateId = dto.OrderItemTemplateId;
    }

    private static ProductUpsertDto MapToDto(Product product) => new()
    {
        Name = product.Name,
        Sku = product.Sku,
        Barcode = product.Barcode,
        PriceAmount = product.PriceAmount,
        PriceCurrency = product.PriceCurrency,
        WeightValue = product.WeightValue,
        WeightUnit = product.WeightUnit,
        ManufactureDate = product.ManufactureDate,
        ExpireDate = product.ExpireDate,
        ShelfLifeDays = product.ShelfLifeDays,
        CategoryId = product.CategoryId,
        DefaultTemplateId = product.DefaultTemplateId,
        OrderItemTemplateId = product.OrderItemTemplateId,
        CustomFieldValues = product.CustomFields.ToDictionary(c => c.FieldDefinitionId, c => c.Value)
    };
}
