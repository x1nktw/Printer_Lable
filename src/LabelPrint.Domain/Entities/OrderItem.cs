using LabelPrint.Domain.Common;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Single line item within an order.
/// </summary>
public class OrderItem : EntityBase
{
    public Guid OrderId { get; set; }

    public Order? Order { get; set; }

    public Guid? ProductId { get; set; }

    public Product? Product { get; set; }

    public string? ExternalProductId { get; set; }

    public string? Sku { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public decimal? Price { get; set; }

    /// <summary>1-based position index for N/M labels.</summary>
    public int PositionIndex { get; set; }

    public int PositionTotal { get; set; }

    public string? Comment { get; set; }

    public bool IsPrinted { get; set; }
}
