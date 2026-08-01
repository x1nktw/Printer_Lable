namespace LabelPrint.Application.Options;

/// <summary>
/// Background print queue worker configuration.
/// </summary>
public sealed class PrintQueueOptions
{
    public const string SectionName = "PrintQueue";

    /// <summary>When true, a background worker processes pending jobs.</summary>
    public bool UseBackgroundWorker { get; set; } = true;

    /// <summary>When true, <see cref="Abstractions.Services.IPrintService.PrintProductAsync"/> processes immediately (tests / fallback).</summary>
    public bool ProcessSynchronously { get; set; }

    /// <summary>Delay between worker poll iterations in milliseconds.</summary>
    public int PollIntervalMs { get; set; } = 500;

    /// <summary>Base delay for exponential backoff on transient retries.</summary>
    public int RetryBackoffBaseMs { get; set; } = 1000;
}
