using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using LabelPrint.Domain.Enums;
using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
using LabelPrint.Plugins.Abstractions.Printing;
using LabelPrint.UI.Services;

namespace LabelPrint.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUiDialogService _dialogs;
    private readonly IUserSession _session;
    private string _currentNavKey = "home";
    private bool _suppressNav;

    public MainViewModel(
        IServiceProvider services,
        IServiceScopeFactory scopeFactory,
        IUiDialogService dialogs,
        IUserSession session)
    {
        _services = services;
        _scopeFactory = scopeFactory;
        _dialogs = dialogs;
        _session = session;
        NavItems = new ObservableCollection<NavItem>
        {
            new("home", "Главная", AppIcons.Home),
            new("catalog", "Каталог", AppIcons.Catalog),
            new("raw", "Маркировка", AppIcons.Tag),
            new("orders", "Заказы", AppIcons.Orders),
            new("settings", "Настройки", AppIcons.Settings)
        };

        CurrentPage = new HomeViewModel();
        _selectedNavItem = NavItems[0];
        _ = InitializeAsync();
    }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty] private PageViewModelBase _currentPage;
    [ObservableProperty] private NavItem? _selectedNavItem;
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] private bool _isSidebarCollapsed;

    public double SidebarWidth => IsSidebarCollapsed ? 60 : 260;
    public double SidebarExpandedOpacity => IsSidebarCollapsed ? 0 : 1;
    public double SidebarCollapsedOpacity => IsSidebarCollapsed ? 1 : 0;
    public string SidebarToggleTooltip => IsSidebarCollapsed ? "Развернуть меню" : "Свернуть меню";
    // Same icon either way — hamburger reads clearer than panel glyphs at 18px
    public Geometry SidebarToggleIcon => AppIcons.PanelLeft;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarExpandedOpacity));
        OnPropertyChanged(nameof(SidebarCollapsedOpacity));
        OnPropertyChanged(nameof(SidebarToggleIcon));
        OnPropertyChanged(nameof(SidebarToggleTooltip));
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (_suppressNav || value is null || value.Key == _currentNavKey)
        {
            return;
        }

        _ = NavigateToAsync(value.Key);
    }

    private async Task InitializeAsync()
    {
        await InitializeThemeAsync();
        await AutoSignInAsync();
        await ResetToHomeAsync(loadStatus: true);
    }

    /// <summary>
    /// Signs in as Administrator (or first active user) without showing a login UI.
    /// Keeps IUserSession for settings roles and reprint attribution.
    /// </summary>
    private async Task AutoSignInAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = await auth.ListUsersAsync();
        if (result.IsFailure || result.Value.Count == 0)
        {
            return;
        }

        var user = result.Value.FirstOrDefault(u => u.Role == UserRole.Administrator)
                   ?? result.Value[0];

        // Seeded users have no PIN; skip users that require one.
        if (user.RequiresPin)
        {
            user = result.Value.FirstOrDefault(u => !u.RequiresPin) ?? user;
            if (user.RequiresPin)
            {
                return;
            }
        }

        await auth.SignInAsync(user.Id, pin: null);
    }

    private async Task ResetToHomeAsync(bool loadStatus)
    {
        _currentNavKey = "home";
        _suppressNav = true;
        SelectedNavItem = NavItems[0];
        _suppressNav = false;

        var home = new HomeViewModel(_scopeFactory);
        DisposeCurrentPage();
        CurrentPage = home;
        if (loadStatus)
        {
            await home.LoadStatusCommand.ExecuteAsync(null);
        }
    }

    private void DisposeCurrentPage()
    {
        if (CurrentPage is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task InitializeThemeAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var result = await settings.GetAsync();
        if (result.IsSuccess)
        {
            IsDarkTheme = result.Value.Theme != AppTheme.Light;
            ThemeApplier.Apply(result.Value.Theme, result.Value.AccentColor);
            return;
        }

        ThemeApplier.Apply(AppTheme.Dark, ThemeApplier.DefaultAccentHex);
    }

    private async Task NavigateToAsync(string key)
    {
        if (!_session.IsSignedIn)
        {
            await AutoSignInAsync();
            if (!_session.IsSignedIn)
            {
                return;
            }
        }

        if (CurrentPage is TemplateEditorViewModel editor)
        {
            if (!await editor.TryLeaveAsync())
            {
                _suppressNav = true;
                SelectedNavItem = NavItems.First(n => n.Key == "settings");
                _suppressNav = false;
                return;
            }
        }

        _currentNavKey = key;

        DisposeCurrentPage();

        try
        {
            switch (key)
            {
                case "home":
                {
                    var home = new HomeViewModel(_scopeFactory);
                    CurrentPage = home;
                    await home.LoadStatusCommand.ExecuteAsync(null);
                    break;
                }
                case "catalog":
                {
                    var catalog = _services.GetRequiredService<CatalogViewModel>();
                    CurrentPage = catalog;
                    await catalog.LoadCommand.ExecuteAsync(null);
                    break;
                }
                case "raw":
                {
                    var raw = _services.GetRequiredService<RawMaterialsViewModel>();
                    CurrentPage = raw;
                    await raw.LoadCommand.ExecuteAsync(null);
                    break;
                }
                case "settings":
                {
                    // Show Settings immediately — LoadAsync must not block navigation (update check / network).
                    var settings = CreateSettingsViewModel();
                    CurrentPage = settings;
                    await settings.LoadCommand.ExecuteAsync(null);
                    break;
                }
                case "orders":
                {
                    var orders = _services.GetRequiredService<OrdersViewModel>();
                    CurrentPage = orders;
                    await orders.LoadCommand.ExecuteAsync(null);
                    break;
                }
                default:
                    CurrentPage = new PlaceholderViewModel(key, "Раздел", "Страница в разработке.");
                    break;
            }
        }
        catch (Exception ex)
        {
            CurrentPage = new PlaceholderViewModel(
                key,
                "Ошибка",
                $"Не удалось открыть раздел: {ex.Message}");
        }
    }

    private SettingsViewModel CreateSettingsViewModel(bool showTemplates = false)
    {
        var settings = _services.GetRequiredService<SettingsViewModel>();
        settings.BindTemplateEditor(OpenTemplateEditor);
        if (showTemplates)
        {
            settings.ShowTemplatesTab();
        }

        return settings;
    }

    private async Task<SettingsViewModel> OpenSettingsAsync(bool showTemplates = false)
    {
        var settings = CreateSettingsViewModel(showTemplates);
        await settings.LoadCommand.ExecuteAsync(null);
        return settings;
    }

    private async void OpenTemplateEditor(Guid templateId)
    {
        var editor = new TemplateEditorViewModel(
            _scopeFactory,
            _dialogs,
            templateId,
            async () =>
            {
                _currentNavKey = "settings";
                _suppressNav = true;
                SelectedNavItem = NavItems.First(n => n.Key == "settings");
                _suppressNav = false;
                DisposeCurrentPage();
                var settings = CreateSettingsViewModel(showTemplates: true);
                CurrentPage = settings;
                await settings.LoadCommand.ExecuteAsync(null);
            });

        DisposeCurrentPage();
        CurrentPage = editor;
        await editor.LoadCommand.ExecuteAsync(null);
        _currentNavKey = "settings";
    }
}

public sealed record NavItem(string Key, string Title, Geometry Icon);

public enum SystemStatusLevel
{
    Ok,
    Warning,
    Error
}

public sealed class SystemStatusItem
{
    public SystemStatusLevel Level { get; init; }

    public string Icon { get; init; } = "✔";

    public string Title { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public IBrush IconBrush => Level switch
    {
        SystemStatusLevel.Ok => new SolidColorBrush(Color.Parse("#34C759")),
        SystemStatusLevel.Warning => new SolidColorBrush(Color.Parse("#FF9F0A")),
        _ => new SolidColorBrush(Color.Parse("#FF453A"))
    };
}

public partial class HomeViewModel : PageViewModelBase, IDisposable
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly CancellationTokenSource _refreshCts = new();
    private int _refreshRunning;

    public HomeViewModel(IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
        Title = "Главная";
    }

    public string Subtitle { get; } =
        "Приложение для каталога товаров, приёма заказов FrontPad и печати этикеток на термопринтерах. " +
        "Управляйте шаблонами, маркировкой сырья, очередью печати и историей из одного окна.";

    public ObservableCollection<SystemStatusItem> StatusItems { get; } = new();

    [ObservableProperty] private string _versionLabel = "LabelPrint Pro";

    public void Dispose()
    {
        _refreshCts.Cancel();
        _refreshCts.Dispose();
    }

    [RelayCommand]
    private async Task LoadStatusAsync()
    {
        await RefreshStatusCoreAsync();
        StartAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        if (Interlocked.Exchange(ref _refreshRunning, 1) == 1)
        {
            return;
        }

        _ = AutoRefreshLoopAsync();
    }

    private async Task AutoRefreshLoopAsync()
    {
        try
        {
            while (!_refreshCts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _refreshCts.Token);
                await RefreshStatusCoreAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // page left
        }
    }

    private async Task RefreshStatusCoreAsync()
    {
        var next = new List<SystemStatusItem>();

        if (_scopeFactory is null)
        {
            next.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Warning,
                Icon = "⚠",
                Title = "Статус недоступен"
            });
            ReplaceStatusItems(next);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            // Local checks first so the strip is useful even if GitHub update check hangs.
            AddBridgeAndWebhookStatus(sp, next);
            AddFrontPadStatus(sp, next);
            await AddPrinterStatusAsync(sp, next);
            await AddQueueStatusAsync(sp, next);
            await AddLastPrintStatusAsync(sp, next);
            ReplaceStatusItems(next);

            await AddUpdateStatusOnceAsync(sp, next);
            if (_cachedUpdateStatusItem is not null && !next.Contains(_cachedUpdateStatusItem))
            {
                next.Insert(0, _cachedUpdateStatusItem);
                ReplaceStatusItems(next);
            }
        }
        catch (Exception ex)
        {
            next.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Error,
                Icon = "❌",
                Title = "Ошибка статуса",
                Detail = ex.Message
            });
            ReplaceStatusItems(next);
        }
    }

    private bool _updateStatusChecked;
    private SystemStatusItem? _cachedUpdateStatusItem;

    private async Task AddUpdateStatusOnceAsync(IServiceProvider sp, List<SystemStatusItem> items)
    {
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        var current = version is null
            ? "0.9.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
        VersionLabel = $"LabelPrint Pro v{current}";

        if (!_updateStatusChecked)
        {
            _updateStatusChecked = true;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var updates = sp.GetRequiredService<IUpdateChecker>();
                var result = await updates.CheckAsync(timeout.Token);
                if (result.IsSuccess && result.Value.UpdateAvailable)
                {
                    _cachedUpdateStatusItem = new SystemStatusItem
                    {
                        Level = SystemStatusLevel.Warning,
                        Icon = "⬆",
                        Title = $"Доступно обновление v{result.Value.LatestVersion}",
                        Detail = "Настройки → Система → Обновить"
                    };
                }
            }
            catch
            {
                // ignore — status strip should not fail on network / Velopack
            }
        }
    }

    private void ReplaceStatusItems(IReadOnlyList<SystemStatusItem> next)
    {
        void Apply()
        {
            StatusItems.Clear();
            foreach (var item in next)
            {
                StatusItems.Add(item);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Dispatcher.UIThread.Post(Apply);
        }
    }

    private static void AddBridgeAndWebhookStatus(IServiceProvider sp, List<SystemStatusItem> items)
    {
        var webhook = sp.GetService<IOrderWebhookHost>();
        if (webhook is null || !webhook.IsListening)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Error,
                Icon = "❌",
                Title = "Webhook недоступен",
                Detail = "Локальный слушатель не запущен"
            });
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Error,
                Icon = "❌",
                Title = "Bridge отключен"
            });
            return;
        }

        var feed = webhook.GetFeedStatus();
        if (!feed.IsBridgeOnline)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Error,
                Icon = "❌",
                Title = "Bridge отключен",
                Detail = "Откройте popup расширения (Обновить) — нужен v1.3.4+"
            });
            return;
        }

        if (feed.BridgeEnabled == false)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Warning,
                Icon = "⚠",
                Title = "Bridge на паузе",
                Detail = "Выключен в расширении"
            });
            return;
        }

        items.Add(new SystemStatusItem
        {
            Level = SystemStatusLevel.Ok,
            Icon = "✔",
            Title = "Bridge подключен"
        });
    }

    private static void AddFrontPadStatus(IServiceProvider sp, List<SystemStatusItem> items)
    {
        var webhook = sp.GetService<IOrderWebhookHost>();
        var feed = webhook?.GetFeedStatus();

        if (feed is null || !feed.IsListening || !feed.IsBridgeOnline)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Error,
                Icon = "❌",
                Title = "FrontPad Offline",
                Detail = "Нет связи через Bridge"
            });
            return;
        }

        if (feed.BridgeEnabled == false)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Warning,
                Icon = "⚠",
                Title = "FrontPad Offline",
                Detail = "Bridge на паузе"
            });
            return;
        }

        if (feed.FrontPadHookActive)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Ok,
                Icon = "✔",
                Title = "FrontPad Online"
            });
            return;
        }

        items.Add(new SystemStatusItem
        {
            Level = SystemStatusLevel.Error,
            Icon = "❌",
            Title = "FrontPad Offline",
            Detail = "Нет открытой вкладки FrontPad"
        });
    }

    private static async Task AddPrinterStatusAsync(IServiceProvider sp, List<SystemStatusItem> items)
    {
        var printers = sp.GetRequiredService<IPrinterService>();
        var list = await printers.ListAsync(includeInactive: false);
        if (list.IsFailure || list.Value.Count == 0)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Warning,
                Icon = "⚠",
                Title = "Нет принтера"
            });
            return;
        }

        var printer = list.Value.FirstOrDefault(p => p.IsDefault) ?? list.Value[0];
        var gateway = sp.GetService<IPrinterGateway>();
        if (gateway is not null)
        {
            try
            {
                var device = await gateway.GetStatusAsync(printer.Id);
                if (!device.IsOnline)
                {
                    items.Add(new SystemStatusItem
                    {
                        Level = SystemStatusLevel.Error,
                        Icon = "❌",
                        Title = "Принтер недоступен",
                        Detail = printer.Name
                    });
                    return;
                }

                if (!device.HasPaper)
                {
                    items.Add(new SystemStatusItem
                    {
                        Level = SystemStatusLevel.Warning,
                        Icon = "⚠",
                        Title = "Принтер",
                        Detail = $"{printer.Name} — нет бумаги"
                    });
                    return;
                }
            }
            catch
            {
                items.Add(new SystemStatusItem
                {
                    Level = SystemStatusLevel.Warning,
                    Icon = "⚠",
                    Title = "Принтер",
                    Detail = printer.Name
                });
                return;
            }
        }

        items.Add(new SystemStatusItem
        {
            Level = SystemStatusLevel.Ok,
            Icon = "✔",
            Title = "Принтер",
            Detail = printer.Name
        });
    }

    private static async Task AddQueueStatusAsync(IServiceProvider sp, List<SystemStatusItem> items)
    {
        var queue = sp.GetRequiredService<IPrintQueueService>();
        var result = await queue.ListAsync();
        var count = result.IsSuccess ? result.Value.Count : 0;
        var hasFailed = result.IsSuccess && result.Value.Any(j => j.Status == PrintJobStatus.Failed);

        items.Add(new SystemStatusItem
        {
            Level = hasFailed ? SystemStatusLevel.Warning : SystemStatusLevel.Ok,
            Icon = hasFailed ? "⚠" : "✔",
            Title = "Очередь",
            Detail = FormatQueueCount(count)
        });
    }

    private static async Task AddLastPrintStatusAsync(IServiceProvider sp, List<SystemStatusItem> items)
    {
        var history = sp.GetRequiredService<IPrintHistoryService>();
        var result = await history.GetPageAsync(cursor: null, pageSize: 1);
        if (result.IsFailure || result.Value.Items.Count == 0)
        {
            items.Add(new SystemStatusItem
            {
                Level = SystemStatusLevel.Warning,
                Icon = "⚠",
                Title = "Последняя печать",
                Detail = "Нет данных"
            });
            return;
        }

        var last = result.Value.Items[0];
        items.Add(new SystemStatusItem
        {
            Level = SystemStatusLevel.Ok,
            Icon = "✔",
            Title = "Последняя печать",
            Detail = last.PrintedAt.ToLocalTime().ToString("HH:mm")
        });
    }

    private static string FormatQueueCount(int count) =>
        count switch
        {
            0 => "0 заданий",
            1 => "1 задание",
            >= 2 and <= 4 => $"{count} задания",
            _ => $"{count} заданий"
        };
}

public partial class PlaceholderViewModel : PageViewModelBase
{
    public PlaceholderViewModel(string key, string title, string message)
    {
        Key = key;
        Title = title;
        Message = message;
    }

    public string Key { get; }

    public string Message { get; }
}
