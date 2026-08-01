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
    public ObservableCollection<TemplateListItemDto> Templates { get; } = new();

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private OrderListItemDto? _selectedOrder;
    [ObservableProperty] private OrderItemRowVm? _selectedItem;
    [ObservableProperty] private TemplateListItemDto? _selectedTemplate;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private OrderStatus? _statusFilter;
    [ObservableProperty] private OrderDetailDto? _selectedOrderDetail;

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
            await LoadTemplatesAsync(scope);

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
                ? "Заказов нет. Сохраните заказ в FrontPad (Bridge) или положите JSON в inbox / «Пример»."
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
            var result = await service.PrintAllItemsAsync(
                SelectedOrder.Id,
                templateId: SelectedTemplate?.Id);
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
            var result = await service.PrintItemsAsync(
                SelectedOrder.Id,
                itemIds,
                templateId: SelectedTemplate?.Id);
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

    private async Task LoadTemplatesAsync(IServiceScope scope)
    {
        var templates = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var result = await templates.SearchAsync(null, includeArchived: false, skip: 0, take: 200);
        Templates.Clear();
        if (result.IsFailure)
        {
            return;
        }

        foreach (var item in result.Value.Items)
        {
            Templates.Add(item);
        }

        SelectedTemplate ??= Templates.FirstOrDefault(t =>
                                t.Name.Contains("Кухня чек", StringComparison.OrdinalIgnoreCase)
                                || t.Name.Contains("Кухня", StringComparison.OrdinalIgnoreCase))
                            ?? Templates.FirstOrDefault();
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
        OrderItemMatchStatus.MatchedBySku => "Артикул",
        OrderItemMatchStatus.MatchedByBarcode => "Штрихкод",
        OrderItemMatchStatus.MatchedByName => "Название",
        _ => "—"
    };

    public bool IsPrinted => Item.IsPrinted;

    [ObservableProperty] private bool _isSelected;
}
