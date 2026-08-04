using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;
using LabelPrint.UI.Models;
using LabelPrint.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

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

        AccentOptions = new ObservableCollection<AccentOption>(
        [
            new("Белый", "#FFFFFF", light: true),
            new("Жемчужный", "#F5F5F4", light: true),
            new("Светло-серый", "#E5E7EB", light: true),
            new("Мятный", "#D1FAE5", light: true),
            new("Небесный", "#DBEAFE", light: true),
            new("Лавандовый", "#EDE9FE", light: true),
            new("Персиковый", "#FFEDD5", light: true),
            new("ChatGPT", "#10A37F"),
            new("Синий", "#3B82F6"),
            new("Бирюзовый", "#14B8A6"),
            new("Оранжевый", "#F97316"),
            new("Красный", "#EF4444"),
            new("Фиолетовый", "#8B5CF6"),
            new("Розовый", "#EC4899")
        ]);
    }

    public PrintersViewModel Printers { get; }
    public QueueViewModel Queue { get; }
    public HistoryViewModel History { get; }
    public TemplatesViewModel Templates { get; }
    public ObservableCollection<AccentOption> AccentOptions { get; }

    [ObservableProperty] private bool _isAdministrator;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private AppTheme _theme = AppTheme.Dark;
    [ObservableProperty] private string _accentColor = ThemeApplier.DefaultAccentHex;
    [ObservableProperty] private AccentOption? _selectedAccent;
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
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private bool _isCheckingUpdates;
    [ObservableProperty] private bool _isInstallingUpdate;
    [ObservableProperty] private double _updateDownloadProgress;
    [ObservableProperty] private UpdateCheckResult? _pendingUpdate;
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

    partial void OnAccentColorChanged(string value)
    {
        RefreshAccentSelection();
        ThemeApplier.ApplyAccent(value);
    }

    partial void OnThemeChanged(AppTheme value) =>
        ThemeApplier.Apply(value, AccentColor);

    partial void OnSelectedAccentChanged(AccentOption? value)
    {
        if (value is null || string.Equals(AccentColor, value.Hex, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AccentColor = value.Hex;
    }

    private void RefreshAccentSelection()
    {
        var current = (AccentColor ?? ThemeApplier.DefaultAccentHex).Trim();
        AccentOption? match = null;
        foreach (var option in AccentOptions)
        {
            option.IsSelected = string.Equals(option.Hex, current, StringComparison.OrdinalIgnoreCase);
            if (option.IsSelected)
            {
                match = option;
            }
        }

        if (!ReferenceEquals(SelectedAccent, match))
        {
            SelectedAccent = match;
        }
    }

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
                // Accent first so theme apply does not briefly reset to default.
                AccentColor = string.IsNullOrWhiteSpace(dto.AccentColor)
                    ? ThemeApplier.DefaultAccentHex
                    : dto.AccentColor;
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
                RefreshAccentSelection();
            }
        }

        // Tabs/content first — update check is network-bound and must not block Settings open.
        await Templates.LoadCommand.ExecuteAsync(null);
        await Printers.LoadCommand.ExecuteAsync(null);
        await Queue.LoadCommand.ExecuteAsync(null);
        await History.LoadCommand.ExecuteAsync(null);

        if (IsAdministrator)
        {
            _ = CheckUpdatesAsync();
        }
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (IsCheckingUpdates || IsInstallingUpdate)
        {
            return;
        }

        IsCheckingUpdates = true;
        UpdateStatus = "Проверка обновлений…";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var updates = scope.ServiceProvider.GetRequiredService<IUpdateChecker>();
            var updateResult = await updates.CheckAsync();
            ApplyUpdateCheck(updateResult);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (IsInstallingUpdate || IsCheckingUpdates)
        {
            return;
        }

        IsInstallingUpdate = true;
        UpdateDownloadProgress = 0;
        UpdateStatus = "Скачивание обновления…";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var updates = scope.ServiceProvider.GetRequiredService<IUpdateChecker>();

            // Refresh check so we know Velopack is available.
            var check = await updates.CheckAsync();
            ApplyUpdateCheck(check);
            if (check.IsFailure)
            {
                return;
            }

            if (!check.Value.IsVelopackInstall)
            {
                UpdateStatus = check.Value.Message;
                if (!string.IsNullOrWhiteSpace(check.Value.ReleasePageUrl))
                {
                    OpenUrl(check.Value.ReleasePageUrl);
                }

                return;
            }

            if (!check.Value.UpdateAvailable)
            {
                UpdateStatus = check.Value.Message;
                return;
            }

            var progress = new Progress<double>(p =>
            {
                UpdateDownloadProgress = p;
                UpdateStatus = $"Скачивание… {(int)(p * 100)}%";
            });

            var apply = await updates.DownloadAndApplyAsync(progress);
            // Success path restarts the process and does not return.
            if (apply.IsFailure)
            {
                UpdateStatus = apply.Error ?? "Не удалось применить обновление.";
            }
        }
        finally
        {
            IsInstallingUpdate = false;
        }
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        var url = PendingUpdate?.ReleasePageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = "https://github.com/x1nktw/Printer_Lable/releases/latest";
        }

        OpenUrl(url);
    }

    private void ApplyUpdateCheck(LabelPrint.Application.Common.Result<UpdateCheckResult> updateResult)
    {
        if (updateResult.IsFailure)
        {
            PendingUpdate = null;
            UpdateAvailable = false;
            UpdateStatus = updateResult.Error ?? "Не удалось проверить обновления.";
            return;
        }

        PendingUpdate = updateResult.Value;
        UpdateAvailable = updateResult.Value.UpdateAvailable;
        UpdateStatus = updateResult.Value.UpdateAvailable
            ? $"v{updateResult.Value.CurrentVersion} → v{updateResult.Value.LatestVersion}: {updateResult.Value.Message}"
            : $"v{updateResult.Value.CurrentVersion} — {updateResult.Value.Message}";
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var result = await settings.SaveAsync(new SettingsDto
        {
            Theme = Theme,
            AccentColor = AccentColor,
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
        if (result.IsSuccess)
        {
            ThemeApplier.Apply(Theme, AccentColor);
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
