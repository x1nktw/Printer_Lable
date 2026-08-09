using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Settings application service.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(IUnitOfWork unitOfWork, ILogger<SettingsService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<SettingsDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _unitOfWork.Settings.GetAsync(cancellationToken);
        return Result.Success(Map(settings));
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(SettingsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.OrdersRefreshIntervalSeconds < 30)
        {
            return Result.Failure("Интервал обновления заказов должен быть не меньше 30 секунд.");
        }

        if (dto.DefaultLabelWidthMm <= 0 || dto.DefaultLabelHeightMm <= 0)
        {
            return Result.Failure("Размер этикетки должен быть больше нуля.");
        }

        var settings = await _unitOfWork.Settings.GetAsync(cancellationToken);
        settings.Theme = dto.Theme;
        settings.AccentColor = NormalizeAccent(dto.AccentColor);
        settings.Language = dto.Language;
        settings.AutoPrintOrders = dto.AutoPrintOrders;
        settings.AutoRefreshOrders = dto.AutoRefreshOrders;
        settings.OrdersRefreshIntervalSeconds = dto.OrdersRefreshIntervalSeconds;
        settings.FrontPadWebhookListenUrl = string.IsNullOrWhiteSpace(dto.FrontPadWebhookListenUrl)
            ? "http://127.0.0.1:8765/"
            : dto.FrontPadWebhookListenUrl.Trim();
        settings.DefaultLabelWidthMm = dto.DefaultLabelWidthMm;
        settings.DefaultLabelHeightMm = dto.DefaultLabelHeightMm;
        settings.MaxPrintRetries = dto.MaxPrintRetries;
        settings.AutoBackupEnabled = dto.AutoBackupEnabled;
        settings.LabelDateTimeMode = dto.LabelDateTimeMode;
        settings.ManualLabelDateTime = dto.LabelDateTimeMode == Domain.Enums.LabelDateTimeMode.Manual
            ? dto.ManualLabelDateTime ?? DateTimeOffset.Now
            : null;
        settings.OrdersPrintTemplateId = dto.OrdersPrintTemplateId;
        settings.OrdersPrintPrinterId = dto.OrdersPrintPrinterId;
        settings.MarkingPrintTemplateId = dto.MarkingPrintTemplateId;
        settings.DatabasePath = string.IsNullOrWhiteSpace(dto.DatabasePath) ? null : dto.DatabasePath.Trim();
        settings.BackupPath = string.IsNullOrWhiteSpace(dto.BackupPath) ? null : dto.BackupPath.Trim();
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Settings.Update(settings);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Application settings saved");
        return Result.Success();
    }

    private SettingsDto Map(AppSettings s) => new()
    {
        Theme = s.Theme,
        AccentColor = string.IsNullOrWhiteSpace(s.AccentColor) ? "#10A37F" : s.AccentColor,
        Language = s.Language,
        AutoPrintOrders = s.AutoPrintOrders,
        AutoRefreshOrders = s.AutoRefreshOrders,
        OrdersRefreshIntervalSeconds = s.OrdersRefreshIntervalSeconds,
        FrontPadWebhookListenUrl = string.IsNullOrWhiteSpace(s.FrontPadWebhookListenUrl)
            ? "http://127.0.0.1:8765/"
            : s.FrontPadWebhookListenUrl,
        DefaultLabelWidthMm = s.DefaultLabelWidthMm,
        DefaultLabelHeightMm = s.DefaultLabelHeightMm,
        MaxPrintRetries = s.MaxPrintRetries,
        AutoBackupEnabled = s.AutoBackupEnabled,
        LabelDateTimeMode = s.LabelDateTimeMode,
        ManualLabelDateTime = s.ManualLabelDateTime ?? DateTimeOffset.Now,
        OrdersPrintTemplateId = s.OrdersPrintTemplateId,
        OrdersPrintPrinterId = s.OrdersPrintPrinterId,
        MarkingPrintTemplateId = s.MarkingPrintTemplateId,
        DatabasePath = s.DatabasePath,
        BackupPath = s.BackupPath,
        DefaultBackupDirectory = ResolveBackupDirectory(s)
    };

    private static string NormalizeAccent(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return "#10A37F";
        }

        var value = hex.Trim();
        if (!value.StartsWith('#'))
        {
            value = "#" + value;
        }

        return value.Length is 7 or 9 ? value.ToUpperInvariant() : "#10A37F";
    }

    private string ResolveBackupDirectory(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BackupPath))
        {
            return Environment.ExpandEnvironmentVariables(settings.BackupPath.Trim());
        }

        var dbPath = !string.IsNullOrWhiteSpace(settings.DatabasePath)
            ? Environment.ExpandEnvironmentVariables(settings.DatabasePath.Trim())
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LabelPrintPro",
                "labelprint.db");

        return Path.Combine(Path.GetDirectoryName(dbPath)!, "backups");
    }
}
