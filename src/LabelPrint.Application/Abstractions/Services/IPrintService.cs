using LabelPrint.Application.Common;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Enqueues and processes label print jobs.
/// </summary>
public interface IPrintService
{
    /// <summary>
    /// Prints a catalog product label using its default template (or first available).
    /// </summary>
    Task<Result<Guid>> PrintProductAsync(
        Guid productId,
        Guid? printerId = null,
        int copies = 1,
        DateTimeOffset? labelDateTimeOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prints a raw-material label (name + date/time) using the «Сырьё» template when available.
    /// </summary>
    Task<Result<Guid>> PrintRawLabelAsync(
        string name,
        Guid? printerId = null,
        int copies = 1,
        DateTimeOffset? labelDateTimeOverride = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a print job for a single order line item.
    /// </summary>
    Task<Result<Guid>> PrintOrderItemAsync(
        Guid orderItemId,
        Guid? printerId = null,
        int copies = 1,
        CancellationToken cancellationToken = default);
}
