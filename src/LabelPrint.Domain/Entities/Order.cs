using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Local order synchronized from an external provider (FrontPad).
/// </summary>
public class Order : EntityBase
{
    /// <summary>External system order id (unique).</summary>
    public string ExternalOrderId { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.New;

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Comment { get; set; }

    public string? Address { get; set; }

    public string? Employee { get; set; }

    public decimal? TotalAmount { get; set; }

    public DateTimeOffset? OrderedAt { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? RawPayloadJson { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
