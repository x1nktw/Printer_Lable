using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// UI-facing order operations: sync, list, detail, print.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrderSyncService _syncService;
    private readonly IPrintService _printService;
    private readonly OrderMatchingService _matchingService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IUnitOfWork unitOfWork,
        OrderSyncService syncService,
        IPrintService printService,
        OrderMatchingService matchingService,
        ILogger<OrderService> logger)
    {
        _unitOfWork = unitOfWork;
        _syncService = syncService;
        _printService = printService;
        _matchingService = matchingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<OrderProviderStatusDto>> GetProviderStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _unitOfWork.Settings.GetAsync(cancellationToken);
        var webhookConfigured = !string.IsNullOrWhiteSpace(settings.FrontPadWebhookListenUrl);

        var inboxPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "orders-inbox");

        var status = new OrderProviderStatusDto
        {
            IsDevelopmentMode = false,
            IsFrontPadConfigured = webhookConfigured,
            IsLiveApiAvailable = false,
            InboxPath = inboxPath,
            BannerMessage =
                """
                Заказы: FrontPad Bridge → локальный webhook → список. Inbox — подстраховка. Shop API не используется.
                """.Trim()
        };

        return Result.Success(status);
    }

    /// <inheritdoc />
    public Task<Result<string>> EnsureSampleInboxOrderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var inbox = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LabelPrintPro",
                "orders-inbox");
            Directory.CreateDirectory(inbox);
            var samplePath = Path.Combine(inbox, $"kitchen-{DateTime.Now:yyyyMMddHHmmss}.json");
            const string sample = """
                {
                  "externalOrderId": "kitchen-demo-001",
                  "number": "42",
                  "customerName": "Зал / Самовывоз",
                  "comment": "Кухонный заказ (демо)",
                  "statusCode": "new",
                  "orderedAt": "2026-08-01T12:00:00+05:00",
                  "items": [
                    { "sku": "001", "name": "Филадельфия", "quantity": 1, "price": 450 },
                    { "sku": "002", "name": "Калифорния", "quantity": 2, "price": 380 }
                  ]
                }
                """;
            File.WriteAllText(samplePath, sample);
            return Task.FromResult(Result.Success($"Кухонный пример: {samplePath}. Нажмите «Синхронизировать» или дождитесь автоопроса."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<string>(ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<OrderSyncSummaryDto>> SyncInboxOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var createdIds = await _syncService.SyncAsync(cancellationToken);
            var message = createdIds.Count > 0
                ? $"Новых кухонных заказов: {createdIds.Count}."
                : "Новых заказов в inbox нет.";
            return Result.Success(new OrderSyncSummaryDto
            {
                OrdersFromInbox = createdIds.Count,
                NewOrdersCreated = createdIds.Count,
                NewOrderIds = createdIds,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inbox order sync failed");
            return Result.Failure<OrderSyncSummaryDto>(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<OrderSyncSummaryDto>> SyncFromProviderAsync(CancellationToken cancellationToken = default)
        => await SyncInboxOrdersAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Result<(IReadOnlyList<OrderListItemDto> Items, int TotalCount)>> SearchAsync(
        string? search,
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var (orders, total) = await _unitOfWork.Orders.SearchAsync(search, status, skip, take, cancellationToken);
        var items = orders.Select(MapListItem).ToList();
        return Result.Success(((IReadOnlyList<OrderListItemDto>)items, total));
    }

    /// <inheritdoc />
    public async Task<Result<OrderDetailDto>> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderDetailDto>("Order not found.");
        }

        var itemDtos = new List<OrderItemDto>();
        foreach (var item in order.Items.OrderBy(i => i.PositionIndex))
        {
            var (_, matchStatus) = await _matchingService.MatchAsync(item.Sku, null, item.Name, cancellationToken);
            var productName = item.ProductId is Guid pid
                ? (await _unitOfWork.Products.GetByIdAsync(pid, cancellationToken))?.Name
                : null;

            itemDtos.Add(new OrderItemDto
            {
                Id = item.Id,
                OrderId = item.OrderId,
                ProductId = item.ProductId,
                ProductName = productName,
                Sku = item.Sku,
                Name = item.Name,
                Quantity = item.Quantity,
                Price = item.Price,
                PositionIndex = item.PositionIndex,
                PositionTotal = item.PositionTotal,
                Comment = item.Comment,
                IsPrinted = item.IsPrinted,
                MatchStatus = item.ProductId is not null
                    ? matchStatus
                    : OrderItemMatchStatus.Unmatched
            });
        }

        return Result.Success(new OrderDetailDto
        {
            Id = order.Id,
            ExternalOrderId = order.ExternalOrderId,
            Number = order.Number,
            Status = order.Status,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            Comment = order.Comment,
            Address = order.Address,
            Employee = order.Employee,
            TotalAmount = order.TotalAmount,
            OrderedAt = order.OrderedAt,
            ReceivedAt = order.ReceivedAt ?? order.CreatedAt,
            Items = itemDtos
        });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Guid>>> PrintItemsAsync(
        Guid orderId,
        IReadOnlyList<Guid> orderItemIds,
        Guid? printerId = null,
        Guid? templateId = null,
        CancellationToken cancellationToken = default)
    {
        if (orderItemIds.Count == 0)
        {
            return Result.Failure<IReadOnlyList<Guid>>("Select at least one item to print.");
        }

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<IReadOnlyList<Guid>>("Order not found.");
        }

        var jobIds = new List<Guid>();
        foreach (var itemId in orderItemIds)
        {
            var item = order.Items.FirstOrDefault(i => i.Id == itemId);
            if (item is null)
            {
                continue;
            }

            var result = await _printService.PrintOrderItemAsync(
                itemId, printerId, templateId: templateId, cancellationToken: cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Guid>>(result.Error!);
            }

            jobIds.Add(result.Value);
        }

        return Result.Success((IReadOnlyList<Guid>)jobIds);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Guid>>> PrintAllItemsAsync(
        Guid orderId,
        Guid? printerId = null,
        Guid? templateId = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<IReadOnlyList<Guid>>("Order not found.");
        }

        var ids = order.Items.Select(i => i.Id).ToList();
        return await PrintItemsAsync(orderId, ids, printerId, templateId, cancellationToken);
    }

    private static OrderListItemDto MapListItem(Domain.Entities.Order order)
    {
        var items = order.Items.ToList();
        return new OrderListItemDto
        {
            Id = order.Id,
            ExternalOrderId = order.ExternalOrderId,
            Number = order.Number,
            Status = order.Status,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            TotalAmount = order.TotalAmount,
            OrderedAt = order.OrderedAt,
            ReceivedAt = order.ReceivedAt ?? order.CreatedAt,
            ItemCount = items.Count,
            MatchedItemCount = items.Count(i => i.ProductId is not null),
            PrintedItemCount = items.Count(i => i.IsPrinted)
        };
    }
}
