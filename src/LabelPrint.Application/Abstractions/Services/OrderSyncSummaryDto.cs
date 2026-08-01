namespace LabelPrint.Application.Abstractions.Services;

/// <summary>Combined sync for Orders UI / poll worker (inbox only).</summary>
public sealed class OrderSyncSummaryDto
{
    public int OrdersFromInbox { get; init; }

    public int NewOrdersCreated { get; init; }

    public IReadOnlyList<Guid> NewOrderIds { get; init; } = Array.Empty<Guid>();

    public string Message { get; init; } = string.Empty;
}
