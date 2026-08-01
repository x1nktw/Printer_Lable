using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Print queue management service.
/// </summary>
public sealed class PrintQueueService : IPrintQueueService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSession _session;
    private readonly ILogger<PrintQueueService> _logger;

    public PrintQueueService(IUnitOfWork unitOfWork, IUserSession session, ILogger<PrintQueueService> logger)
    {
        _unitOfWork = unitOfWork;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PrintQueueItemDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _unitOfWork.PrintJobs.ListQueueAsync(cancellationToken);
        var printers = await _unitOfWork.Printers.GetAllAsync(includeInactive: true, cancellationToken);
        var printerNames = printers.ToDictionary(p => p.Id, p => p.Name);

        var items = jobs.Select(j => Map(j, printerNames)).ToList();
        return Result.Success<IReadOnlyList<PrintQueueItemDto>>(items);
    }

    /// <inheritdoc />
    public async Task<Result> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.PrintJobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure("Задание не найдено.");
        }

        try
        {
            job.Cancel();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        _unitOfWork.PrintJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Print job {JobId} cancelled", jobId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.PrintJobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure("Задание не найдено.");
        }

        if (job.Status != PrintJobStatus.Failed || !job.IsTransientFailure)
        {
            return Result.Failure("Повтор доступен только для временных сбоев.");
        }

        try
        {
            job.RequeueForRetry();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        _unitOfWork.PrintJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Print job {JobId} manually requeued", jobId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> ReprintJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.PrintJobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure<Guid>("Задание не найдено.");
        }

        try
        {
            var reprint = job.CreateReprint(_session.CurrentUserId, _session.CurrentUserName);
            await _unitOfWork.PrintJobs.AddAsync(reprint, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Reprint job {NewJobId} created from {SourceJobId}", reprint.Id, jobId);
            return Result.Success(reprint.Id);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }
    }

    private static PrintQueueItemDto Map(PrintJob job, IReadOnlyDictionary<Guid, string> printerNames)
    {
        printerNames.TryGetValue(job.PrinterId, out var name);
        name ??= job.Printer?.Name ?? "—";

        return new PrintQueueItemDto
        {
            Id = job.Id,
            Title = job.Title,
            Status = job.Status,
            PrinterId = job.PrinterId,
            PrinterName = name,
            Copies = job.Copies,
            RetryCount = job.RetryCount,
            FailureReason = job.FailureReason,
            IsTransientFailure = job.IsTransientFailure,
            CreatedAt = job.CreatedAt,
            CanCancel = job.Status is PrintJobStatus.Pending or PrintJobStatus.Failed,
            CanRetry = job.Status == PrintJobStatus.Failed && job.IsTransientFailure
        };
    }
}
