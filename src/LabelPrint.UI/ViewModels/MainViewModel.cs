using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Avalonia.Media;
using LabelPrint.Domain.Enums;
using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.DTOs;
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
    public ObservableCollection<UserListItemDto> Users { get; } = new();

    [ObservableProperty] private PageViewModelBase _currentPage;
    [ObservableProperty] private NavItem? _selectedNavItem;
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _currentUserLabel = "Гость";
    [ObservableProperty] private UserListItemDto? _selectedLoginUser;
    [ObservableProperty] private string? _loginPin;
    [ObservableProperty] private string? _loginError;

    public double SidebarWidth => IsSidebarCollapsed ? 60 : 260;
    public double SidebarExpandedOpacity => IsSidebarCollapsed ? 0 : 1;
    public double SidebarCollapsedOpacity => IsSidebarCollapsed ? 1 : 0;
    public double LoginOverlayOpacity => IsSignedIn ? 0 : 1;
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

    partial void OnIsSignedInChanged(bool value) =>
        OnPropertyChanged(nameof(LoginOverlayOpacity));

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

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (SelectedLoginUser is null)
        {
            LoginError = "Выберите пользователя.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = await auth.SignInAsync(SelectedLoginUser.Id, LoginPin);
        if (result.IsFailure)
        {
            LoginError = result.Error;
            return;
        }

        LoginPin = null;
        LoginError = null;
        RefreshSessionUi();
        await ResetToHomeAsync(loadAbout: true);
    }

    [RelayCommand]
    private void SignOut()
    {
        using var scope = _scopeFactory.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        auth.SignOut();
        ResetToHome(loadAbout: false);
        RefreshSessionUi();
    }

    private void ResetToHome(bool loadAbout)
    {
        _currentNavKey = "home";
        _suppressNav = true;
        SelectedNavItem = NavItems[0];
        _suppressNav = false;

        var home = new HomeViewModel(_scopeFactory);
        CurrentPage = home;
        if (loadAbout)
        {
            _ = home.LoadAboutCommand.ExecuteAsync(null);
        }
    }

    private async Task ResetToHomeAsync(bool loadAbout)
    {
        _currentNavKey = "home";
        _suppressNav = true;
        SelectedNavItem = NavItems[0];
        _suppressNav = false;

        var home = new HomeViewModel(_scopeFactory);
        CurrentPage = home;
        if (loadAbout)
        {
            await home.LoadAboutCommand.ExecuteAsync(null);
        }
    }

    private async Task InitializeAsync()
    {
        await InitializeThemeAsync();
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = await auth.ListUsersAsync();
        Users.Clear();
        if (result.IsFailure)
        {
            LoginError = result.Error;
            return;
        }

        foreach (var user in result.Value)
        {
            Users.Add(user);
        }

        SelectedLoginUser = Users.FirstOrDefault();
    }

    private void RefreshSessionUi()
    {
        IsSignedIn = _session.IsSignedIn;
        CurrentUserLabel = _session.IsSignedIn
            ? $"{_session.CurrentUserName} ({_session.CurrentUserRole})"
            : "Гость";
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
        if (!IsSignedIn)
        {
            return;
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

        switch (key)
        {
            case "home":
            {
                var home = new HomeViewModel(_scopeFactory);
                CurrentPage = home;
                await home.LoadAboutCommand.ExecuteAsync(null);
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
                CurrentPage = await OpenSettingsAsync();
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

    private async Task<SettingsViewModel> OpenSettingsAsync(bool showTemplates = false)
    {
        var settings = _services.GetRequiredService<SettingsViewModel>();
        settings.BindTemplateEditor(OpenTemplateEditor);
        if (showTemplates)
        {
            settings.ShowTemplatesTab();
        }

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
                CurrentPage = await OpenSettingsAsync(showTemplates: true);
            });

        CurrentPage = editor;
        await editor.LoadCommand.ExecuteAsync(null);
        _currentNavKey = "settings";
    }
}

public sealed record NavItem(string Key, string Title, Geometry Icon);

public partial class HomeViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory? _scopeFactory;

    public HomeViewModel(IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
        Title = "Главная";
    }

    public string Subtitle { get; } =
        "Приложение для каталога товаров, приёма заказов FrontPad и печати этикеток на термопринтерах. " +
        "Управляйте шаблонами, маркировкой сырья, очередью печати и историей из одного окна.";

    [ObservableProperty] private string _versionLabel = "LabelPrint Pro";
    [ObservableProperty] private string _updateMessage = "Проверка обновлений…";

    [RelayCommand]
    private async Task LoadAboutAsync()
    {
        if (_scopeFactory is null)
        {
            UpdateMessage = "Updates not configured.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var updates = scope.ServiceProvider.GetRequiredService<IUpdateChecker>();
        var result = await updates.CheckAsync();
        if (result.IsFailure)
        {
            UpdateMessage = result.Error ?? "Не удалось проверить обновления.";
            return;
        }

        VersionLabel = $"LabelPrint Pro v{result.Value.CurrentVersion}";
        UpdateMessage = result.Value.Message;
    }
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
