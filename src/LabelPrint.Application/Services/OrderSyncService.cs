using System.Text.Json;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Plugins.Abstractions.Orders;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Pulls orders from <see cref="IOrderProvider"/> and upserts by unique ExternalOrderId.
/// </summary>
public sealed class OrderSyncService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderProvider _orderProvider;
    private readonly OrderMatchingService _matchingService;
    private readonly ILogger<OrderSyncService> _logger;

    public OrderSyncService(
        IUnitOfWork unitOfWork,
        IOrderProvider orderProvider,
        OrderMatchingService matchingService,
        ILogger<OrderSyncService> logger)
    {
        _unitOfWork = unitOfWork;
        _orderProvider = orderProvider;
        _matchingService = matchingService;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes orders from the provider. Returns ids of newly created orders (not updates).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> SyncAsync(CancellationToken cancellationToken = default)
    {
        var externalOrders = await _orderProvider.GetNewOrdersAsync(cancellationToken);
        var createdIds = new List<Guid>();
        var touched = 0;

        foreach (var external in externalOrders)
        {
            if (string.IsNullOrWhiteSpace(external.ExternalOrderId))
            {
                _logger.LogWarning("Skipping order without ExternalOrderId");
                continue;
            }

            var existing = await _unitOfWork.Orders.GetByExternalOrderIdAsync(
                external.ExternalOrderId,
                cancellationToken);

            if (existing is null)
            {
                var order = MapNewOrder(external);
                ReplaceItems(order, external.Items);
                for (var i = 0; i < order.Items.Count && i < external.Items.Count; i++)
                {
                    await ApplyMatchFromExternalAsync(order.Items.ElementAt(i), external.Items[i], cancellationToken);
                }

                await _unitOfWork.Orders.AddAsync(order, cancellationToken);
                createdIds.Add(order.Id);
                touched++;
            }
            else
            {
                UpdateOrder(existing, external);
                ReplaceItems(existing, external.Items);
                for (var i = 0; i < existing.Items.Count && i < external.Items.Count; i++)
                {
                    await ApplyMatchFromExternalAsync(existing.Items.ElementAt(i), external.Items[i], cancellationToken);
                }

                _unitOfWork.Orders.Update(existing);
                touched++;
            }

            await _orderProvider.AcknowledgeAsync(external.ExternalOrderId, cancellationToken);
        }

        if (touched > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Synced {Touched} orders ({Created} new) from provider {ProviderKey}",
                touched, createdIds.Count, _orderProvider.ProviderKey);
        }

        return createdIds;
    }

    private static Order MapNewOrder(ExternalOrderDto external)
    {
        var now = DateTimeOffset.UtcNow;
        return new Order
        {
            ExternalOrderId = external.ExternalOrderId,
            Number = external.Number,
            Status = MapStatus(external.StatusCode),
            CustomerName = external.CustomerName,
            CustomerPhone = external.CustomerPhone,
            Comment = external.Comment,
            Address = external.Address,
            Employee = external.Employee,
            TotalAmount = external.TotalAmount,
            OrderedAt = external.OrderedAt,
            ReceivedAt = now,
            RawPayloadJson = external.RawPayloadJson ?? JsonSerializer.Serialize(external)
        };
    }

    private static void UpdateOrder(Order order, ExternalOrderDto external)
    {
        order.Number = external.Number;
        order.Status = MapStatus(external.StatusCode);
        order.CustomerName = external.CustomerName;
        order.CustomerPhone = external.CustomerPhone;
        order.Comment = external.Comment;
        order.Address = external.Address;
        order.Employee = external.Employee;
        order.TotalAmount = external.TotalAmount;
        order.OrderedAt = external.OrderedAt;
        order.RawPayloadJson = external.RawPayloadJson ?? JsonSerializer.Serialize(external);
        order.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ReplaceItems(Order order, IReadOnlyList<ExternalOrderItemDto> externalItems)
    {
        order.Items.Clear();
        var total = externalItems.Count;
        var index = 1;

        foreach (var item in externalItems)
        {
            order.Items.Add(new OrderItem
            {
                ExternalProductId = item.ExternalProductId,
                Sku = item.Sku,
                Name = item.Name,
                Quantity = item.Quantity,
                Price = item.Price,
                Comment = item.Comment,
                PositionIndex = index,
                PositionTotal = total
            });
            index++;
        }
    }

    private async Task ApplyMatchFromExternalAsync(
        OrderItem item,
        ExternalOrderItemDto external,
        CancellationToken cancellationToken)
    {
        var (product, _) = await _matchingService.MatchAsync(
            item.Sku,
            external.Barcode ?? item.Sku,
            item.Name,
            cancellationToken);
        item.ProductId = product?.Id;
    }

    internal static OrderStatus MapStatus(string? statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            return OrderStatus.New;
        }

        return statusCode.Trim().ToLowerInvariant() switch
        {
            "new" or "0" => OrderStatus.New,
            "confirmed" or "1" => OrderStatus.Confirmed,
            "in_progress" or "inprogress" or "2" => OrderStatus.InProgress,
            "ready" or "3" => OrderStatus.Ready,
            "delivering" or "4" => OrderStatus.Delivering,
            "completed" or "done" or "5" => OrderStatus.Completed,
            "cancelled" or "canceled" or "6" => OrderStatus.Cancelled,
            _ => OrderStatus.Unknown
        };
    }
}
