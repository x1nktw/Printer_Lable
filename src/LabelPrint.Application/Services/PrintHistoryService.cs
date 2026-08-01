using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Print history browsing and reprint service.
/// </summary>
public sealed class PrintHistoryService : IPrintHistoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSession _session;
    private readonly ILogger<PrintHistoryService> _logger;

    public PrintHistoryService(IUnitOfWork unitOfWork, IUserSession session, ILogger<PrintHistoryService> logger)
    {
        _unitOfWork = unitOfWork;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<CursorPage<PrintHistoryItemDto>>> GetPageAsync(
        string? cursor,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageSize < 1 || pageSize > 200)
        {
            return Result.Failure<CursorPage<PrintHistoryItemDto>>("Размер страницы должен быть от 1 до 200.");
        }

        DateTimeOffset? before = null;
        if (!string.IsNullOrWhiteSpace(cursor) && long.TryParse(cursor, out var ticks))
        {
            before = new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        var page = await _unitOfWork.PrintHistory.GetPageAsync(before, pageSize, cancellationToken);
        var items = page.Items.Select(Map).ToList();
        return Result.Success(new CursorPage<PrintHistoryItemDto>(items, page.NextCursor, page.HasMore));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> ReprintAsync(Guid historyId, CancellationToken cancellationToken = default)
    {
        var entry = await _unitOfWork.PrintHistory.GetByIdAsync(historyId, cancellationToken);
        if (entry is null)
        {
            return Result.Failure<Guid>("Запись истории не найдена.");
        }

        if (entry.PrintJobId is Guid sourceJobId)
        {
            var sourceJob = await _unitOfWork.PrintJobs.GetByIdAsync(sourceJobId, cancellationToken);
            if (sourceJob is not null)
            {
                try
                {
                    var reprint = sourceJob.CreateReprint(_session.CurrentUserId, _session.CurrentUserName);
                    await _unitOfWork.PrintJobs.AddAsync(reprint, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Reprint job {NewJobId} from history {HistoryId}", reprint.Id, historyId);
                    return Result.Success(reprint.Id);
                }
                catch (DomainException ex)
                {
                    return Result.Failure<Guid>(ex.Message);
                }
            }
        }

        if (entry.PrinterId is null || entry.TemplateId is null)
        {
            return Result.Failure<Guid>("Недостаточно данных для повторной печати.");
        }

        var printer = await _unitOfWork.Printers.GetByIdAsync(entry.PrinterId.Value, cancellationToken);
        if (printer is null || !printer.IsActive)
        {
            return Result.Failure<Guid>("Принтер недоступен.");
        }

        var job = new PrintJob
        {
            PrinterId = entry.PrinterId.Value,
            TemplateId = entry.TemplateId,
            ProductId = entry.ProductId,
            OrderId = entry.OrderId,
            OrderItemId = entry.OrderItemId,
            Copies = entry.Copies,
            Title = entry.Description,
            VariablesJson = entry.VariablesJson,
            SourceJobId = entry.SourceJobId ?? entry.PrintJobId,
            RequestedByUserId = _session.CurrentUserId,
            RequestedByName = _session.CurrentUserName
        };

        await _unitOfWork.PrintJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Reprint job {NewJobId} from history snapshot {HistoryId}", job.Id, historyId);
        return Result.Success(job.Id);
    }

    private static PrintHistoryItemDto Map(PrintHistory entry) => new()
    {
        Id = entry.Id,
        PrintedAt = entry.PrintedAt,
        Status = entry.Status,
        Description = entry.Description,
        PrinterName = entry.PrinterName,
        ProductName = entry.ProductName,
        Copies = entry.Copies,
        FailureReason = entry.FailureReason
    };
}
