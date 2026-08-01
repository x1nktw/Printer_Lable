using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Order persistence port.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Order?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken cancellationToken = default);

    Task<OrderItem?> GetItemByIdAsync(Guid orderItemId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
        string? search,
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    void Update(Order order);
}
