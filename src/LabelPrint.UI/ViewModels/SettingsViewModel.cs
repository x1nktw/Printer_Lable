using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;
using LabelPrint.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LabelPrint.UI.ViewModels;

public partial class SettingsViewModel : PageViewModelBase
{
    public const int TemplatesTabIndex = 1;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUiDialogService _dialogs;

    public SettingsViewModel(
        IServiceProvider services,
        IServiceScopeFactory scopeFactory,
        IUserSession session,
        IUiDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _dialogs = dialogs;
        Title = "Настройки";
        IsAdministrator = session.CurrentUserRole == UserRole.Administrator;
        SelectedTabIndex = IsAdministrator ? 0 : TemplatesTabIndex;
        Printers = services.GetRequiredService<PrintersViewModel>();
        Queue = services.GetRequiredService<QueueViewModel>();
        History = services.GetRequiredService<HistoryViewModel>();
        Templates = new TemplatesViewModel(_scopeFactory, _dialogs);
    }

    public PrintersViewModel Printers { get; }
    public QueueViewModel Queue { get; }
    public HistoryViewModel History { get; }
    public TemplatesViewModel Templates { get; }

    [ObservableProperty] private bool _isAdministrator;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private AppTheme _theme = AppTheme.Dark;
    [ObservableProperty] private AppLanguage _language = AppLanguage.Russian;
    [ObservableProperty] private bool _autoPrintOrders;
    [ObservableProperty] private bool _autoRefreshOrders = true;
    [ObservableProperty] private int _ordersRefreshIntervalSeconds = 120;
    [ObservableProperty] private string? _frontPadWebhookListenUrl = "http://127.0.0.1:8765/";
    [ObservableProperty] private double _defaultLabelWidthMm = 58;
    [ObservableProperty] private double _defaultLabelHeightMm = 40;
    [ObservableProperty] private int _maxPrintRetries = 3;
    [ObservableProperty] private bool _autoBackupEnabled = true;
    [ObservableProperty] private string? _databasePath;
    [ObservableProperty] private string? _backupPath;
    [ObservableProperty] private string _defaultBackupDirectory = string.Empty;
    private Guid? _ordersPrintTemplateId;
    private Guid? _markingPrintTemplateId;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private LabelDateTimeMode _labelDateTimeMode = LabelDateTimeMode.Realtime;
    [ObservableProperty] private DateTime? _manualLabelDate = DateTime.Today;
    [ObservableProperty] private TimeSpan? _manualLabelTime = DateTime.Now.TimeOfDay;

    public Array Themes { get; } = Enum.GetValues(typeof(AppTheme));
    public Array Languages { get; } = Enum.GetValues(typeof(AppLanguage));
    public Array LabelDateTimeModes { get; } = Enum.GetValues(typeof(LabelDateTimeMode));

    public bool IsManualLabelDateTime => LabelDateTimeMode == LabelDateTimeMode.Manual;

    public void BindTemplateEditor(Action<Guid> openEditor) => Templates.BindOpenEditor(openEditor);

    public void ShowTemplatesTab() => SelectedTabIndex = TemplatesTabIndex;

    partial void OnLabelDateTimeModeChanged(LabelDateTimeMode value) =>
        OnPropertyChanged(nameof(IsManualLabelDateTime));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsAdministrator)
        {
            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var result = await settings.GetAsync();
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
            }
            else
            {
                var dto = result.Value;
                Theme = dto.Theme;
                Language = dto.Language;
                AutoPrintOrders = dto.AutoPrintOrders;
                AutoRefreshOrders = dto.AutoRefreshOrders;
                OrdersRefreshIntervalSeconds = dto.OrdersRefreshIntervalSeconds;
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
                _ordersPrintTemplateId = dto.OrdersPrintTemplateId;
                _markingPrintTemplateId = dto.MarkingPrintTemplateId;
                StatusMessage = "Загружено";

                var updates = scope.ServiceProvider.GetRequiredService<IUpdateChecker>();
                var updateResult = await updates.CheckAsync();
                UpdateStatus = updateResult.IsSuccess
                    ? $"v{updateResult.Value.CurrentVersion} — {updateResult.Value.Message}"
                    : updateResult.Error ?? "Не удалось проверить обновления.";
            }
        }

        await Templates.LoadCommand.ExecuteAsync(null);
        await Printers.LoadCommand.ExecuteAsync(null);
        await Queue.LoadCommand.ExecuteAsync(null);
        await History.LoadCommand.ExecuteAsync(null);
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
            FrontPadWebhookListenUrl = FrontPadWebhookListenUrl,
            DefaultLabelWidthMm = DefaultLabelWidthMm,
            DefaultLabelHeightMm = DefaultLabelHeightMm,
            MaxPrintRetries = MaxPrintRetries,
            AutoBackupEnabled = AutoBackupEnabled,
            LabelDateTimeMode = LabelDateTimeMode,
            ManualLabelDateTime = new DateTimeOffset(
                (ManualLabelDate ?? DateTime.Today).Date.Add(ManualLabelTime ?? TimeSpan.Zero)),
            OrdersPrintTemplateId = _ordersPrintTemplateId,
            MarkingPrintTemplateId = _markingPrintTemplateId,
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
