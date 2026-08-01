using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Print history persistence port with keyset pagination.
/// </summary>
public interface IPrintHistoryRepository
{
    Task<PrintHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(PrintHistory entry, CancellationToken cancellationToken = default);

    Task<CursorPage<PrintHistory>> GetPageAsync(
        DateTimeOffset? before,
        int pageSize,
        CancellationToken cancellationToken = default);
}
