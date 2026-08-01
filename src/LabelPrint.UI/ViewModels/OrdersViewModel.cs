using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LabelPrint.UI.ViewModels;

public partial class OrdersViewModel : PageViewModelBase
{
    private const int PageSize = 50;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOrderFeedNotifier _feedNotifier;
    private int _silentReload;

    public OrdersViewModel(IServiceScopeFactory scopeFactory, IOrderFeedNotifier feedNotifier)
    {
        _scopeFactory = scopeFactory;
        _feedNotifier = feedNotifier;
        Title = "Заказы";
        _feedNotifier.OrdersChanged += OnOrdersChanged;
    }

    private void OnOrdersChanged(object? sender, EventArgs e)
    {
        // Marshal to UI thread — webhook runs on background thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = ReloadQuietAsync());
    }

    /// <summary>Refreshes the list without blocking Sync buttons / clearing selection when possible.</summary>
    private async Task ReloadQuietAsync()
    {
        if (Interlocked.Exchange(ref _silentReload, 1) == 1)
        {
            return;
        }

        try
        {
            var selectedId = SelectedOrder?.Id;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.SearchAsync(SearchText, StatusFilter, skip: 0, take: PageSize);
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            Orders.Clear();
            foreach (var order in result.Value.Items)
            {
                Orders.Add(order);
            }

            if (selectedId is Guid id)
            {
                SelectedOrder = Orders.FirstOrDefault(o => o.Id == id);
            }

            StatusMessage = result.Value.TotalCount == 0
                ? "Заказов нет."
                : $"Заказов: {result.Value.TotalCount} (автообновление)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка автообновления: {ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _silentReload, 0);
        }
    }

    public ObservableCollection<OrderListItemDto> Orders { get; } = new();
    public ObservableCollection<OrderItemRowVm> ItemRows { get; } = new();
    public ObservableCollection<ProductListItemDto> ProductHits { get; } = new();
    public ObservableCollection<KitchenDraftLineVm> DraftLines { get; } = new();

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private OrderListItemDto? _selectedOrder;
    [ObservableProperty] private OrderItemRowVm? _selectedItem;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _bannerMessage = "Кухонные заказы FrontPad — через JSON-inbox / webhook / ручное создание.";
    [ObservableProperty] private string? _inboxPath;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private OrderStatus? _statusFilter;
    [ObservableProperty] private OrderDetailDto? _selectedOrderDetail;
    [ObservableProperty] private string _kitchenOrderNumber = string.Empty;
    [ObservableProperty] private string? _productSearchText;
    [ObservableProperty] private ProductListItemDto? _selectedProductHit;
    [ObservableProperty] private bool _isKitchenPanelExpanded;

    partial void OnSelectedOrderChanged(OrderListItemDto? value) => _ = LoadOrderDetailAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

            var statusResult = await service.GetProviderStatusAsync();
            if (statusResult.IsSuccess)
            {
                BannerMessage = statusResult.Value.BannerMessage;
                InboxPath = statusResult.Value.InboxPath;
            }

            var result = await service.SearchAsync(SearchText, StatusFilter, skip: 0, take: PageSize);
            Orders.Clear();
            ItemRows.Clear();
            SelectedOrderDetail = null;
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            foreach (var order in result.Value.Items)
            {
                Orders.Add(order);
            }

            StatusMessage = result.Value.TotalCount == 0
                ? "Заказов нет. Inbox / webhook с составом, ручное создание или «Пример кухонного»."
                : $"Заказов: {result.Value.TotalCount}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.SyncFromProviderAsync();
            StatusMessage = result.IsFailure
                ? result.Error
                : result.Value.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка синхронизации: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task SyncInboxOnlyAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.SyncInboxOrdersAsync();
            StatusMessage = result.IsFailure
                ? result.Error
                : result.Value.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка inbox: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task CreateSampleAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.EnsureSampleInboxOrderAsync();
            StatusMessage = result.IsFailure ? result.Error : result.Value;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SearchProductsAsync()
    {
        ProductHits.Clear();
        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var result = await products.SearchAsync(ProductSearchText, null, includeArchived: false, skip: 0, take: 20);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        foreach (var item in result.Value.Items)
        {
            ProductHits.Add(item);
        }

        StatusMessage = result.Value.TotalCount == 0
            ? "Товары не найдены. Сначала синхронизируйте каталог (get_products)."
            : $"Найдено: {result.Value.Items.Count}";
    }

    [RelayCommand]
    private void AddSelectedProductToDraft()
    {
        if (SelectedProductHit is null)
        {
            StatusMessage = "Выберите товар из поиска.";
            return;
        }

        var existing = DraftLines.FirstOrDefault(d => d.ProductId == SelectedProductHit.Id);
        if (existing is not null)
        {
            existing.Quantity += 1;
            StatusMessage = $"Количество «{existing.Name}»: {existing.Quantity}";
            return;
        }

        DraftLines.Add(new KitchenDraftLineVm
        {
            ProductId = SelectedProductHit.Id,
            Name = SelectedProductHit.Name,
            Sku = SelectedProductHit.Sku,
            Quantity = 1,
            Price = SelectedProductHit.PriceAmount
        });
        StatusMessage = $"Добавлено: {SelectedProductHit.Name}";
    }

    [RelayCommand]
    private void ClearDraft()
    {
        DraftLines.Clear();
        StatusMessage = "Черновик очищен.";
    }

    [RelayCommand]
    private void RemoveDraftLine(KitchenDraftLineVm? line)
    {
        if (line is null)
        {
            return;
        }

        DraftLines.Remove(line);
    }

    [RelayCommand]
    private async Task CreateKitchenOrderAsync()
    {
        if (DraftLines.Count == 0)
        {
            StatusMessage = "Добавьте позиции из каталога.";
            return;
        }

        var number = string.IsNullOrWhiteSpace(KitchenOrderNumber)
            ? DateTime.Now.ToString("HHmm")
            : KitchenOrderNumber.Trim();

        var lines = DraftLines.Select(d => new KitchenOrderLineDto
        {
            ProductId = d.ProductId,
            Name = d.Name,
            Sku = d.Sku,
            Quantity = d.Quantity,
            Price = d.Price
        }).ToList();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.CreateKitchenOrderAsync(number, lines);
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            DraftLines.Clear();
            KitchenOrderNumber = string.Empty;
            StatusMessage = $"Кухонный заказ №{number} создан.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PrintSelectedItemsAsync()
    {
        if (SelectedOrder is null)
        {
            StatusMessage = "Выберите заказ.";
            return;
        }

        var selectedIds = ItemRows.Where(r => r.IsSelected).Select(r => r.Item.Id).ToList();
        if (selectedIds.Count == 0 && SelectedItem is not null)
        {
            selectedIds.Add(SelectedItem.Item.Id);
        }

        if (selectedIds.Count == 0)
        {
            StatusMessage = "Отметьте позиции галочкой для печати.";
            return;
        }

        await PrintItemsInternalAsync(selectedIds);
    }

    [RelayCommand]
    private async Task PrintAllItemsAsync()
    {
        if (SelectedOrder is null)
        {
            StatusMessage = "Выберите заказ.";
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.PrintAllItemsAsync(SelectedOrder.Id);
            StatusMessage = result.IsFailure
                ? result.Error
                : $"В очередь: {result.Value.Count} заданий";
            await LoadOrderDetailAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task PrintItemsInternalAsync(IReadOnlyList<Guid> itemIds)
    {
        if (SelectedOrder is null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.PrintItemsAsync(SelectedOrder.Id, itemIds);
            StatusMessage = result.IsFailure
                ? result.Error
                : $"В очередь: {result.Value.Count} заданий";
            await LoadOrderDetailAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadOrderDetailAsync()
    {
        ItemRows.Clear();
        SelectedOrderDetail = null;

        if (SelectedOrder is null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var result = await service.GetAsync(SelectedOrder.Id);
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            SelectedOrderDetail = result.Value;
            foreach (var item in result.Value.Items)
            {
                ItemRows.Add(new OrderItemRowVm(item));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

public sealed partial class OrderItemRowVm : ObservableObject
{
    public OrderItemRowVm(OrderItemDto item) => Item = item;

    public OrderItemDto Item { get; }

    public string PositionLabel => $"{Item.PositionIndex}/{Item.PositionTotal}";

    public string Name => Item.Name;

    public string? Sku => Item.Sku;

    public decimal Quantity => Item.Quantity;

    public string MatchLabel => Item.MatchStatus switch
    {
        OrderItemMatchStatus.MatchedBySku => "SKU",
        OrderItemMatchStatus.MatchedByBarcode => "Штрихкод",
        OrderItemMatchStatus.MatchedByName => "Название",
        _ => "Нет"
    };

    public bool IsPrinted => Item.IsPrinted;

    [ObservableProperty] private bool _isSelected;
}

public sealed partial class KitchenDraftLineVm : ObservableObject
{
    public Guid? ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Sku { get; init; }

    public decimal? Price { get; init; }

    [ObservableProperty] private decimal _quantity = 1;
}
