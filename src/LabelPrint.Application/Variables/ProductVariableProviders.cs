using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Plugins.Abstractions.Variables;

namespace LabelPrint.Application.Variables;

/// <summary>
/// Resolves product name for template bindings (ProductName / Name).
/// </summary>
public sealed class ProductNameVariableProvider : ProductFieldVariableProvider
{
    public ProductNameVariableProvider(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string Key => "ProductName";

    public override string DisplayName => "Название товара";

    protected override string? ResolveFromProduct(Domain.Entities.Product product) => product.Name;
}

/// <summary>
/// Resolves SKU for template bindings.
/// </summary>
public sealed class SkuVariableProvider : ProductFieldVariableProvider
{
    public SkuVariableProvider(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string Key => "Sku";

    public override string DisplayName => "Артикул (SKU)";

    protected override string? ResolveFromProduct(Domain.Entities.Product product) => product.Sku;
}

/// <summary>
/// Resolves barcode for template bindings.
/// </summary>
public sealed class BarcodeVariableProvider : ProductFieldVariableProvider
{
    public BarcodeVariableProvider(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string Key => "Barcode";

    public override string DisplayName => "Штрихкод";

    protected override string? ResolveFromProduct(Domain.Entities.Product product) => product.Barcode;
}

/// <summary>
/// Resolves formatted price for template bindings.
/// </summary>
public sealed class PriceVariableProvider : ProductFieldVariableProvider
{
    public PriceVariableProvider(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string Key => "Price";

    public override string DisplayName => "Цена";

    protected override string? ResolveFromProduct(Domain.Entities.Product product) =>
        product.GetPrice().ToString();
}

/// <summary>
/// Base helper for product-scoped variable providers.
/// </summary>
public abstract class ProductFieldVariableProvider : IVariableProvider
{
    private readonly IUnitOfWork _unitOfWork;

    protected ProductFieldVariableProvider(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public abstract string Key { get; }

    public abstract string DisplayName { get; }

    public async Task<string> ResolveAsync(VariableContext context, CancellationToken cancellationToken = default)
    {
        if (context.Values.TryGetValue(Key, out var explicitValue))
        {
            return explicitValue;
        }

        if (context.ProductId is not Guid productId)
        {
            return string.Empty;
        }

        var product = await _unitOfWork.Products.GetByIdAsync(productId, cancellationToken);
        return product is null ? string.Empty : ResolveFromProduct(product) ?? string.Empty;
    }

    protected abstract string? ResolveFromProduct(Domain.Entities.Product product);
}

/// <summary>Label date (dd.MM.yyyy) from print context or ILabelDateTimeService.</summary>
public sealed class DateVariableProvider : IVariableProvider
{
    private readonly ILabelDateTimeService _labelDateTime;

    public DateVariableProvider(ILabelDateTimeService labelDateTime) => _labelDateTime = labelDateTime;

    public string Key => "Date";

    public string DisplayName => "Дата";

    public async Task<string> ResolveAsync(VariableContext context, CancellationToken cancellationToken = default)
    {
        if (context.Values.TryGetValue(Key, out var explicitValue) && !string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        var effective = await _labelDateTime.GetEffectiveAsync(cancellationToken: cancellationToken);
        return _labelDateTime.FormatDate(effective);
    }
}

/// <summary>Label time (HH:mm) from print context or ILabelDateTimeService.</summary>
public sealed class TimeVariableProvider : IVariableProvider
{
    private readonly ILabelDateTimeService _labelDateTime;

    public TimeVariableProvider(ILabelDateTimeService labelDateTime) => _labelDateTime = labelDateTime;

    public string Key => "Time";

    public string DisplayName => "Время";

    public async Task<string> ResolveAsync(VariableContext context, CancellationToken cancellationToken = default)
    {
        if (context.Values.TryGetValue(Key, out var explicitValue) && !string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        var effective = await _labelDateTime.GetEffectiveAsync(cancellationToken: cancellationToken);
        return _labelDateTime.FormatTime(effective);
    }
}

/// <summary>Product expire date or context override.</summary>
public sealed class ExpireDateVariableProvider : ProductFieldVariableProvider
{
    public ExpireDateVariableProvider(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string Key => "ExpireDate";

    public override string DisplayName => "Срок годности";

    protected override string? ResolveFromProduct(Domain.Entities.Product product) =>
        product.ExpireDate?.ToString("dd.MM.yyyy");
}
