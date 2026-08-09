using LabelPrint.Application.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Application settings service.
/// </summary>
public interface ISettingsService
{
    Task<Result<SettingsDto>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result> SaveAsync(SettingsDto dto, CancellationToken cancellationToken = default);
}

/// <summary>Settings DTO for UI.</summary>
public sealed class SettingsDto
{
    public AppTheme Theme { get; set; }

    /// <summary>Fluent accent color hex (#RRGGBB).</summary>
    public string AccentColor { get; set; } = "#10A37F";

    public AppLanguage Language { get; set; }

    public bool AutoPrintOrders { get; set; }

    public bool AutoRefreshOrders { get; set; }

    public int OrdersRefreshIntervalSeconds { get; set; }

    public string? FrontPadWebhookListenUrl { get; set; }

    public double DefaultLabelWidthMm { get; set; }

    public double DefaultLabelHeightMm { get; set; }

    public int MaxPrintRetries { get; set; }

    public bool AutoBackupEnabled { get; set; }

    public LabelDateTimeMode LabelDateTimeMode { get; set; } = LabelDateTimeMode.Realtime;

    public DateTimeOffset? ManualLabelDateTime { get; set; }

    public Guid? OrdersPrintTemplateId { get; set; }

    public Guid? OrdersPrintPrinterId { get; set; }

    public Guid? MarkingPrintTemplateId { get; set; }

    /// <summary>Optional override for SQLite database file path.</summary>
    public string? DatabasePath { get; set; }

    /// <summary>Optional override for pre-migration backup directory.</summary>
    public string? BackupPath { get; set; }

    /// <summary>Effective backup directory (configured or default next to database).</summary>
    public string DefaultBackupDirectory { get; set; } = string.Empty;
}
