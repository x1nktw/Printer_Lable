using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Print job queue persistence port.
/// </summary>
public interface IPrintJobRepository
{
    Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PrintJob?> TryClaimNextAsync(Guid printerId, Guid expectedRowVersion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintJob>> GetByStatusAsync(PrintJobStatus status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintJob>> ListQueueAsync(CancellationToken cancellationToken = default);

    Task AddAsync(PrintJob job, CancellationToken cancellationToken = default);

    void Update(PrintJob job);
}
