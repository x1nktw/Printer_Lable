using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.DTOs;

/// <summary>Order list row for the Orders UI.</summary>
public sealed class OrderListItemDto
{
    public Guid Id { get; init; }

    public string ExternalOrderId { get; init; } = string.Empty;

    public string Number { get; init; } = string.Empty;

    public OrderStatus Status { get; init; }

    public string StatusLabel => Status switch
    {
        OrderStatus.New => "Новый",
        OrderStatus.Confirmed => "Подтверждён",
        OrderStatus.InProgress => "В работе",
        OrderStatus.Ready => "Готов",
        OrderStatus.Delivering => "Доставка",
        OrderStatus.Completed => "Выполнен",
        OrderStatus.Cancelled => "Отменён",
        _ => "—"
    };

    public string? CustomerName { get; init; }

    public string? CustomerPhone { get; init; }

    public decimal? TotalAmount { get; init; }

    public DateTimeOffset? OrderedAt { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }

    public int ItemCount { get; init; }

    public int MatchedItemCount { get; init; }

    public int PrintedItemCount { get; init; }
}

/// <summary>Full order with line items.</summary>
public sealed class OrderDetailDto
{
    public Guid Id { get; init; }

    public string ExternalOrderId { get; init; } = string.Empty;

    public string Number { get; init; } = string.Empty;

    public OrderStatus Status { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerPhone { get; init; }

    public string? Comment { get; init; }

    public string? Address { get; init; }

    public string? Employee { get; init; }

    public decimal? TotalAmount { get; init; }

    public DateTimeOffset? OrderedAt { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }

    public IReadOnlyList<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();
}

/// <summary>Single order line item.</summary>
public sealed class OrderItemDto
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public Guid? ProductId { get; init; }

    public string? ProductName { get; init; }

    public string? Sku { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal? Price { get; init; }

    public int PositionIndex { get; init; }

    public int PositionTotal { get; init; }

    public string? Comment { get; init; }

    public bool IsPrinted { get; init; }

    public OrderItemMatchStatus MatchStatus { get; init; }
}

/// <summary>How an order line was matched to the catalog.</summary>
public enum OrderItemMatchStatus
{
    Unmatched = 0,
    MatchedBySku = 1,
    MatchedByBarcode = 2,
    MatchedByName = 3
}

/// <summary>Provider/sync status for the Orders UI banner.</summary>
public sealed class OrderProviderStatusDto
{
    public string BannerMessage { get; init; } = string.Empty;

    public bool IsDevelopmentMode { get; init; }

    /// <summary>True when webhook listen URL is set (Bridge path).</summary>
    public bool IsFrontPadConfigured { get; init; }

    /// <summary>Always false — shop API is not used.</summary>
    public bool IsLiveApiAvailable { get; init; }

    public string? InboxPath { get; init; }
}
