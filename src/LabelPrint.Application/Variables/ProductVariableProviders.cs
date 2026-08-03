using System.Globalization;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;
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
/// Resolves storage temperature regime for marking labels.
/// </summary>
public sealed class TemperatureRegimeVariableProvider : ProductFieldVariableProvider
{
    public TemperatureRegimeVariableProvider(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string Key => "TemperatureRegime";

    public override string DisplayName => "Температурный режим";

    protected override string? ResolveFromProduct(Domain.Entities.Product product) => product.TemperatureRegime;
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

/// <summary>Product expire date/time from fixed date, shelf life, or context override.</summary>
public sealed class ExpireDateVariableProvider : IVariableProvider
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILabelDateTimeService _labelDateTime;

    public ExpireDateVariableProvider(IUnitOfWork unitOfWork, ILabelDateTimeService labelDateTime)
    {
        _unitOfWork = unitOfWork;
        _labelDateTime = labelDateTime;
    }

    public string Key => "ExpireDate";

    public string DisplayName => "Срок годности";

    public async Task<string> ResolveAsync(VariableContext context, CancellationToken cancellationToken = default)
    {
        if (context.Values.TryGetValue(Key, out var explicitValue) && !string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        Domain.Entities.Product? product = null;
        if (context.ProductId is Guid productId)
        {
            product = await _unitOfWork.Products.GetByIdAsync(productId, cancellationToken);
        }

        if (product?.ExpireDate is DateOnly fixedExpire)
        {
            return fixedExpire.ToString("dd.MM.yyyy");
        }

        if (product?.ShelfLifeDays is int value && value > 0)
        {
            var baseDt = await ResolveBaseDateTimeAsync(context, cancellationToken);
            if (product.ShelfLifeUnit == ShelfLifeUnit.Hours)
            {
                return baseDt.AddHours(value).ToString("dd.MM.yyyy HH:mm");
            }

            return DateOnly.FromDateTime(baseDt).AddDays(value).ToString("dd.MM.yyyy");
        }

        return string.Empty;
    }

    private async Task<DateTime> ResolveBaseDateTimeAsync(VariableContext context, CancellationToken cancellationToken)
    {
        DateOnly? date = null;
        if (context.Values.TryGetValue("Date", out var dateText)
            && DateOnly.TryParseExact(dateText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            date = parsedDate;
        }

        TimeSpan time = TimeSpan.Zero;
        if (context.Values.TryGetValue("Time", out var timeText)
            && TimeSpan.TryParseExact(timeText, @"hh\:mm", CultureInfo.InvariantCulture, out var parsedTime))
        {
            time = parsedTime;
        }
        else if (context.Values.TryGetValue("Time", out timeText)
                 && TimeSpan.TryParse(timeText, CultureInfo.InvariantCulture, out parsedTime))
        {
            time = parsedTime;
        }

        if (date is not null)
        {
            return date.Value.ToDateTime(TimeOnly.FromTimeSpan(time));
        }

        var effective = await _labelDateTime.GetEffectiveAsync(cancellationToken: cancellationToken);
        return effective.ToLocalTime().DateTime;
    }
}
