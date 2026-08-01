using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LabelPrint.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly LabelPrintDbContext _db;

    public OrderRepository(LabelPrintDbContext db) => _db = db;

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken cancellationToken = default) =>
        _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ExternalOrderId == externalOrderId, cancellationToken);

    public async Task<OrderItem?> GetItemByIdAsync(Guid orderItemId, CancellationToken cancellationToken = default)
    {
        var item = await _db.OrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == orderItemId, cancellationToken);

        return item;
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
        string? search,
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();

        if (status is OrderStatus filterStatus)
        {
            query = query.Where(o => o.Status == filterStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                EF.Functions.Like(o.Number, $"%{term}%") ||
                EF.Functions.Like(o.ExternalOrderId, $"%{term}%") ||
                (o.CustomerName != null && EF.Functions.Like(o.CustomerName, $"%{term}%")) ||
                (o.CustomerPhone != null && EF.Functions.Like(o.CustomerPhone, $"%{term}%")));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.OrderedAt ?? o.ReceivedAt)
            .ThenByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
        await _db.Orders.AddAsync(order, cancellationToken);

    public void Update(Order order) => _db.Orders.Update(order);
}
