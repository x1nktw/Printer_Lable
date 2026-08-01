using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LabelPrint.UI.ViewModels;

public partial class SettingsViewModel : PageViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Title = "Настройки";
    }

    [ObservableProperty] private AppTheme _theme = AppTheme.Dark;
    [ObservableProperty] private AppLanguage _language = AppLanguage.Russian;
    [ObservableProperty] private bool _autoPrintOrders;
    [ObservableProperty] private bool _autoRefreshOrders = true;
    [ObservableProperty] private int _ordersRefreshIntervalSeconds = 120;
    [ObservableProperty] private string? _frontPadSecret;
    [ObservableProperty] private string _frontPadBaseUrl = "https://app.frontpad.ru/api/index.php";
    [ObservableProperty] private string? _frontPadWebhookListenUrl = "http://127.0.0.1:8765/";
    [ObservableProperty] private double _defaultLabelWidthMm = 58;
    [ObservableProperty] private double _defaultLabelHeightMm = 40;
    [ObservableProperty] private int _maxPrintRetries = 3;
    [ObservableProperty] private bool _autoBackupEnabled = true;
    [ObservableProperty] private string? _databasePath;
    [ObservableProperty] private string? _backupPath;
    [ObservableProperty] private string _defaultBackupDirectory = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private LabelDateTimeMode _labelDateTimeMode = LabelDateTimeMode.Realtime;
    [ObservableProperty] private DateTime? _manualLabelDate = DateTime.Today;
    [ObservableProperty] private TimeSpan? _manualLabelTime = DateTime.Now.TimeOfDay;

    public Array Themes { get; } = Enum.GetValues(typeof(AppTheme));
    public Array Languages { get; } = Enum.GetValues(typeof(AppLanguage));
    public Array LabelDateTimeModes { get; } = Enum.GetValues(typeof(LabelDateTimeMode));

    public bool IsManualLabelDateTime => LabelDateTimeMode == LabelDateTimeMode.Manual;

    partial void OnLabelDateTimeModeChanged(LabelDateTimeMode value) =>
        OnPropertyChanged(nameof(IsManualLabelDateTime));

    [RelayCommand]
    private async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var result = await settings.GetAsync();
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var dto = result.Value;
        Theme = dto.Theme;
        Language = dto.Language;
        AutoPrintOrders = dto.AutoPrintOrders;
        AutoRefreshOrders = dto.AutoRefreshOrders;
        OrdersRefreshIntervalSeconds = dto.OrdersRefreshIntervalSeconds;
        FrontPadSecret = dto.FrontPadSecret;
        FrontPadBaseUrl = dto.FrontPadBaseUrl;
        FrontPadWebhookListenUrl = dto.FrontPadWebhookListenUrl;
        DefaultLabelWidthMm = dto.DefaultLabelWidthMm;
        DefaultLabelHeightMm = dto.DefaultLabelHeightMm;
        MaxPrintRetries = dto.MaxPrintRetries;
        AutoBackupEnabled = dto.AutoBackupEnabled;
        LabelDateTimeMode = dto.LabelDateTimeMode;
        var manual = (dto.ManualLabelDateTime ?? DateTimeOffset.Now).LocalDateTime;
        ManualLabelDate = manual.Date;
        ManualLabelTime = manual.TimeOfDay;
        DatabasePath = dto.DatabasePath;
        BackupPath = dto.BackupPath;
        DefaultBackupDirectory = dto.DefaultBackupDirectory;
        StatusMessage = "Загружено";

        var updates = scope.ServiceProvider.GetRequiredService<IUpdateChecker>();
        var updateResult = await updates.CheckAsync();
        UpdateStatus = updateResult.IsSuccess
            ? $"v{updateResult.Value.CurrentVersion} — {updateResult.Value.Message}"
            : updateResult.Error ?? "Не удалось проверить обновления.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var result = await settings.SaveAsync(new SettingsDto
        {
            Theme = Theme,
            Language = Language,
            AutoPrintOrders = AutoPrintOrders,
            AutoRefreshOrders = AutoRefreshOrders,
            OrdersRefreshIntervalSeconds = OrdersRefreshIntervalSeconds,
            FrontPadSecret = FrontPadSecret,
            FrontPadBaseUrl = FrontPadBaseUrl,
            FrontPadWebhookListenUrl = FrontPadWebhookListenUrl,
            DefaultLabelWidthMm = DefaultLabelWidthMm,
            DefaultLabelHeightMm = DefaultLabelHeightMm,
            MaxPrintRetries = MaxPrintRetries,
            AutoBackupEnabled = AutoBackupEnabled,
            LabelDateTimeMode = LabelDateTimeMode,
            ManualLabelDateTime = new DateTimeOffset(
                (ManualLabelDate ?? DateTime.Today).Date.Add(ManualLabelTime ?? TimeSpan.Zero)),
            DatabasePath = DatabasePath,
            BackupPath = BackupPath,
            DefaultBackupDirectory = DefaultBackupDirectory
        });

        StatusMessage = result.IsFailure ? result.Error : "Сохранено";
        if (result.IsSuccess && Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = Theme == AppTheme.Light
                ? Avalonia.Styling.ThemeVariant.Light
                : Avalonia.Styling.ThemeVariant.Dark;
        }

        if (result.IsSuccess)
        {
            try
            {
                var webhook = Program.Services.GetService<IOrderWebhookHost>();
                webhook?.Stop();
                webhook?.Start();
                if (StatusMessage == "Сохранено")
                {
                    StatusMessage = "Сохранено (webhook перезапущен)";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Сохранено, но webhook не перезапущен: {ex.Message}";
            }
        }
    }
}
