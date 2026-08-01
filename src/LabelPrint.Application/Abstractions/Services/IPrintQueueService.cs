using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Print queue management API.
/// </summary>
public interface IPrintQueueService
{
    Task<Result<IReadOnlyList<PrintQueueItemDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<Result> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> ReprintJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
