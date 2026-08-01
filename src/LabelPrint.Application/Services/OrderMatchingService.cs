using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Services;

/// <summary>
/// Matches order line items to catalog products: Sku → Barcode → Name.
/// </summary>
public sealed class OrderMatchingService
{
    private readonly IUnitOfWork _unitOfWork;

    public OrderMatchingService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    /// <summary>
    /// Attempts to match a line item to a catalog product without creating new products.
    /// </summary>
    public async Task<(Product? Product, OrderItemMatchStatus MatchStatus)> MatchAsync(
        string? sku,
        string? barcode,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var bySku = await _unitOfWork.Products.GetBySkuAsync(sku.Trim(), cancellationToken);
            if (bySku is not null && !bySku.IsArchived)
            {
                return (bySku, OrderItemMatchStatus.MatchedBySku);
            }
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var byBarcode = await _unitOfWork.Products.GetByBarcodeAsync(barcode.Trim(), cancellationToken);
            if (byBarcode is not null && !byBarcode.IsArchived)
            {
                return (byBarcode, OrderItemMatchStatus.MatchedByBarcode);
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var byName = await _unitOfWork.Products.GetByNameExactAsync(name.Trim(), cancellationToken);
            if (byName is not null && !byName.IsArchived)
            {
                return (byName, OrderItemMatchStatus.MatchedByName);
            }
        }

        return (null, OrderItemMatchStatus.Unmatched);
    }

    /// <summary>
    /// Applies catalog match to an order item entity.
    /// </summary>
    public async Task<OrderItemMatchStatus> ApplyMatchAsync(
        OrderItem item,
        CancellationToken cancellationToken = default)
    {
        var (product, status) = await MatchAsync(item.Sku, item.Sku, item.Name, cancellationToken);
        item.ProductId = product?.Id;
        return status;
    }
}
