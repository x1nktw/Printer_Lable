using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Avalonia.Styling;
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
            new("home", "Главная"),
            new("catalog", "Каталог"),
            new("raw", "Сырьё"),
            new("templates", "Шаблоны"),
            new("orders", "Заказы"),
            new("printers", "Принтеры"),
            new("queue", "Очередь печати"),
            new("history", "История"),
            new("settings", "Настройки")
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
    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _currentUserLabel = "Гость";
    [ObservableProperty] private UserListItemDto? _selectedLoginUser;
    [ObservableProperty] private string? _loginPin;
    [ObservableProperty] private string? _loginError;

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
    }

    [RelayCommand]
    private void SignOut()
    {
        using var scope = _scopeFactory.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        auth.SignOut();
        RefreshSessionUi();
        _ = NavigateToAsync("home");
        _suppressNav = true;
        SelectedNavItem = NavItems[0];
        _suppressNav = false;
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
        }

        ApplyTheme(IsDarkTheme);
    }

    private void ApplyTheme(bool dark)
    {
        if (Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    private async Task NavigateToAsync(string key)
    {
        if (!IsSignedIn)
        {
            return;
        }

        if (key == "settings" && _session.CurrentUserRole != UserRole.Administrator)
        {
            CurrentPage = new PlaceholderViewModel(key, "Настройки",
                "Раздел доступен только администратору.");
            _currentNavKey = key;
            return;
        }

        if (CurrentPage is TemplateEditorViewModel editor)
        {
            if (!await editor.TryLeaveAsync())
            {
                _suppressNav = true;
                SelectedNavItem = NavItems.First(n => n.Key == "templates");
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
            case "templates":
                await OpenTemplatesAsync();
                break;
            case "settings":
            {
                var settings = new SettingsViewModel(_scopeFactory);
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
            case "printers":
            {
                var printers = _services.GetRequiredService<PrintersViewModel>();
                CurrentPage = printers;
                await printers.LoadCommand.ExecuteAsync(null);
                break;
            }
            case "queue":
            {
                var queue = _services.GetRequiredService<QueueViewModel>();
                CurrentPage = queue;
                await queue.LoadCommand.ExecuteAsync(null);
                break;
            }
            case "history":
            {
                var history = _services.GetRequiredService<HistoryViewModel>();
                CurrentPage = history;
                await history.LoadCommand.ExecuteAsync(null);
                break;
            }
            default:
                CurrentPage = new PlaceholderViewModel(key, "Раздел", "Страница в разработке.");
                break;
        }
    }

    private async Task OpenTemplatesAsync()
    {
        var templates = new TemplatesViewModel(_scopeFactory, _dialogs, OpenTemplateEditor);
        CurrentPage = templates;
        await templates.LoadCommand.ExecuteAsync(null);
    }

    private async void OpenTemplateEditor(Guid templateId)
    {
        var editor = new TemplateEditorViewModel(
            _scopeFactory,
            _dialogs,
            templateId,
            async () =>
            {
                _currentNavKey = "templates";
                _suppressNav = true;
                SelectedNavItem = NavItems.First(n => n.Key == "templates");
                _suppressNav = false;
                await OpenTemplatesAsync();
            });

        CurrentPage = editor;
        await editor.LoadCommand.ExecuteAsync(null);
        _currentNavKey = "templates";
    }
}

public sealed record NavItem(string Key, string Title);

public partial class HomeViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory? _scopeFactory;

    public HomeViewModel(IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
        Title = "Главная";
    }

    public string Subtitle { get; } =
        "LabelPrint Pro — каталог, шаблоны и печать. Войдите как Администратор или Оператор, чтобы начать работу.";

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
