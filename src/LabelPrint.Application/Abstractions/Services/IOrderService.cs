using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Order list, sync, and print operations for the UI.
/// </summary>
public interface IOrderService
{
    Task<Result<OrderProviderStatusDto>> GetProviderStatusAsync(CancellationToken cancellationToken = default);

    Task<Result<OrderSyncSummaryDto>> SyncFromProviderAsync(CancellationToken cancellationToken = default);

    /// <summary>Syncs kitchen/inbox orders (Bridge webhook / JSON files). For background poll.</summary>
    Task<Result<OrderSyncSummaryDto>> SyncInboxOrdersAsync(CancellationToken cancellationToken = default);

    Task<Result<(IReadOnlyList<OrderListItemDto> Items, int TotalCount)>> SearchAsync(
        string? search,
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<OrderDetailDto>> GetAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<Guid>>> PrintItemsAsync(
        Guid orderId,
        IReadOnlyList<Guid> orderItemIds,
        Guid? printerId = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<Guid>>> PrintAllItemsAsync(
        Guid orderId,
        Guid? printerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a sample JSON order into the local inbox (dev mode) if missing.
    /// </summary>
    Task<Result<string>> EnsureSampleInboxOrderAsync(CancellationToken cancellationToken = default);
}
