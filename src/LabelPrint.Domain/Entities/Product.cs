using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.ValueObjects;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Catalog product aggregate root.
/// </summary>
public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique SKU / article.</summary>
    public string Sku { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    /// <summary>Price amount (currency stored separately for EF simplicity).</summary>
    public decimal PriceAmount { get; set; }

    public string PriceCurrency { get; set; } = "RUB";

    public decimal? WeightValue { get; set; }

    public WeightUnit WeightUnit { get; set; } = WeightUnit.Kilogram;

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpireDate { get; set; }

    public int? ShelfLifeDays { get; set; }

    public Guid? CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Default template for catalog marking.</summary>
    public Guid? DefaultTemplateId { get; set; }

    public LabelTemplate? DefaultTemplate { get; set; }

    /// <summary>Template for order-item labels; falls back to <see cref="DefaultTemplateId"/>.</summary>
    public Guid? OrderItemTemplateId { get; set; }

    public LabelTemplate? OrderItemTemplate { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<ProductCustomField> CustomFields { get; set; } = new List<ProductCustomField>();

    /// <summary>Returns price as a value object.</summary>
    public Money GetPrice() => new(PriceAmount, PriceCurrency);

    /// <summary>Applies price value object.</summary>
    public void SetPrice(Money money)
    {
        PriceAmount = money.Amount;
        PriceCurrency = money.Currency;
    }

    /// <summary>Returns weight value object when present.</summary>
    public Weight? GetWeight() =>
        WeightValue is null ? null : new Weight(WeightValue.Value, WeightUnit);

    /// <summary>Applies weight value object.</summary>
    public void SetWeight(Weight? weight)
    {
        if (weight is null)
        {
            WeightValue = null;
            WeightUnit = WeightUnit.Kilogram;
            return;
        }

        WeightValue = weight.Value;
        WeightUnit = weight.Unit;
    }

    /// <summary>Resolves template id for an order-item print scenario.</summary>
    public Guid? ResolveOrderItemTemplateId() => OrderItemTemplateId ?? DefaultTemplateId;
}
