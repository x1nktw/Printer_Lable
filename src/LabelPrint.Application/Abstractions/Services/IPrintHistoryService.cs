using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Print history browsing and reprint API.
/// </summary>
public interface IPrintHistoryService
{
    Task<Result<CursorPage<PrintHistoryItemDto>>> GetPageAsync(
        string? cursor,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> ReprintAsync(Guid historyId, CancellationToken cancellationToken = default);
}
