using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.DTOs;

/// <summary>Product create/update payload.</summary>
public sealed class ProductUpsertDto
{
    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public decimal PriceAmount { get; set; }

    public string PriceCurrency { get; set; } = "RUB";

    public decimal? WeightValue { get; set; }

    public WeightUnit WeightUnit { get; set; } = WeightUnit.Kilogram;

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpireDate { get; set; }

    public int? ShelfLifeDays { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? DefaultTemplateId { get; set; }

    public Guid? OrderItemTemplateId { get; set; }

    public Dictionary<Guid, string?> CustomFieldValues { get; set; } = new();
}

/// <summary>Product list item for UI grids.</summary>
public sealed class ProductListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string? Barcode { get; init; }

    public decimal PriceAmount { get; init; }

    public string? CategoryName { get; init; }

    public bool IsArchived { get; init; }
}
