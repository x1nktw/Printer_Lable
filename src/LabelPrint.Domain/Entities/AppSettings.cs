using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Application-wide settings singleton row.
/// </summary>
public class AppSettings : EntityBase
{
    public string? DatabasePath { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Fluent accent color as #RRGGBB hex (e.g. #10A37F).</summary>
    public string AccentColor { get; set; } = "#10A37F";

    public AppLanguage Language { get; set; } = AppLanguage.Russian;

    public bool AutoPrintOrders { get; set; }

    public bool AutoRefreshOrders { get; set; } = true;

    /// <summary>Orders sync interval in seconds (inbox poll default ~120).</summary>
    public int OrdersRefreshIntervalSeconds { get; set; } = 120;

    /// <summary>Legacy shop-API secret (unused; kept for DB compatibility).</summary>
    public string? FrontPadSecret { get; set; }

    /// <summary>Legacy shop-API base URL (unused; kept for DB compatibility).</summary>
    public string FrontPadBaseUrl { get; set; } = "https://app.frontpad.ru/api/index.php";

    /// <summary>Local HTTP listen URL for FrontPad Bridge webhook.</summary>
    public string? FrontPadWebhookListenUrl { get; set; } = "http://127.0.0.1:8765/";

    public string? StoragePath { get; set; }

    public string? BackupPath { get; set; }

    public bool AutoBackupEnabled { get; set; } = true;

    public int BackupIntervalHours { get; set; } = 24;

    public int HistoryRetentionMonths { get; set; } = 12;

    public double DefaultLabelWidthMm { get; set; } = 58;

    public double DefaultLabelHeightMm { get; set; } = 40;

    public bool EditorSnapEnabled { get; set; } = true;

    public double EditorGridSizeMm { get; set; } = 1;

    public int MaxPrintRetries { get; set; } = 3;

    /// <summary>Realtime wall clock vs fixed manual stamp on labels.</summary>
    public LabelDateTimeMode LabelDateTimeMode { get; set; } = LabelDateTimeMode.Realtime;

    /// <summary>Used when <see cref="LabelDateTimeMode"/> is Manual.</summary>
    public DateTimeOffset? ManualLabelDateTime { get; set; }

    /// <summary>Template selected on Orders page for kitchen label print.</summary>
    public Guid? OrdersPrintTemplateId { get; set; }

    /// <summary>Printer selected on Orders page for kitchen label print.</summary>
    public Guid? OrdersPrintPrinterId { get; set; }

    /// <summary>Template selected on Marking (сырьё) page for label print.</summary>
    public Guid? MarkingPrintTemplateId { get; set; }
}
