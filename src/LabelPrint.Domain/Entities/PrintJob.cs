using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Exceptions;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Print queue aggregate root with enforced state transitions.
/// </summary>
public class PrintJob : EntityBase
{
    public PrintJobStatus Status { get; private set; } = PrintJobStatus.Pending;

    public Guid PrinterId { get; set; }

    public Printer? Printer { get; set; }

    public Guid? TemplateId { get; set; }

    public LabelTemplate? Template { get; set; }

    public Guid? ProductId { get; set; }

    public Product? Product { get; set; }

    public Guid? OrderId { get; set; }

    public Order? Order { get; set; }

    public Guid? OrderItemId { get; set; }

    public OrderItem? OrderItem { get; set; }

    public int Copies { get; set; } = 1;

    public int Priority { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid? RequestedByUserId { get; set; }

    public User? RequestedByUser { get; set; }

    public string? RequestedByName { get; set; }

    /// <summary>Resolved variables snapshot for reprint fidelity.</summary>
    public string VariablesJson { get; set; } = "{}";

    public string? FailureReason { get; set; }

    public int RetryCount { get; set; }

    public bool IsTransientFailure { get; set; }

    /// <summary>Source job when this job was created via Reprint.</summary>
    public Guid? SourceJobId { get; set; }

    public string? ExternalOrderId { get; set; }

    /// <summary>Optimistic concurrency token for queue claiming.</summary>
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Transitions Pending → Rendering.</summary>
    public void MarkAsRendering()
    {
        EnsureStatus(PrintJobStatus.Pending);
        Status = PrintJobStatus.Rendering;
        StartedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>Transitions Rendering → Printing.</summary>
    public void MarkAsPrinting()
    {
        EnsureStatus(PrintJobStatus.Rendering);
        Status = PrintJobStatus.Printing;
        Touch();
    }

    /// <summary>Transitions Printing → Completed.</summary>
    public void MarkAsCompleted()
    {
        EnsureStatus(PrintJobStatus.Printing);
        Status = PrintJobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        FailureReason = null;
        Touch();
    }

    /// <summary>Marks the job as failed from Rendering or Printing.</summary>
    public void MarkAsFailed(string reason, bool isTransient)
    {
        if (Status is not (PrintJobStatus.Rendering or PrintJobStatus.Printing or PrintJobStatus.Pending))
        {
            throw new DomainException($"Cannot fail print job in status {Status}.");
        }

        Status = PrintJobStatus.Failed;
        FailureReason = reason;
        IsTransientFailure = isTransient;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>Cancels a pending or failed job.</summary>
    public void Cancel()
    {
        if (Status is not (PrintJobStatus.Pending or PrintJobStatus.Failed))
        {
            throw new DomainException($"Cannot cancel print job in status {Status}.");
        }

        Status = PrintJobStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Requeues a failed transient job back to Pending for retry.
    /// Does not mutate completed jobs — use Reprint for those.
    /// </summary>
    public void RequeueForRetry()
    {
        EnsureStatus(PrintJobStatus.Failed);
        if (!IsTransientFailure)
        {
            throw new DomainException("Permanent failures cannot be auto-retried.");
        }

        Status = PrintJobStatus.Pending;
        RetryCount++;
        FailureReason = null;
        StartedAt = null;
        CompletedAt = null;
        RowVersion = Guid.NewGuid();
        Touch();
    }

    /// <summary>
    /// Creates a new print job for reprinting without mutating this instance.
    /// </summary>
    public PrintJob CreateReprint(Guid? requestedByUserId, string? requestedByName)
    {
        if (Status is not (PrintJobStatus.Completed or PrintJobStatus.Failed or PrintJobStatus.Cancelled))
        {
            throw new DomainException("Only finished jobs can be reprinted.");
        }

        return new PrintJob
        {
            PrinterId = PrinterId,
            TemplateId = TemplateId,
            ProductId = ProductId,
            OrderId = OrderId,
            OrderItemId = OrderItemId,
            Copies = Copies,
            Priority = Priority,
            Title = Title,
            VariablesJson = VariablesJson,
            ExternalOrderId = ExternalOrderId,
            SourceJobId = Id,
            RequestedByUserId = requestedByUserId,
            RequestedByName = requestedByName,
            Status = PrintJobStatus.Pending
        };
    }

    private void EnsureStatus(PrintJobStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainException($"Expected status {expected}, but was {Status}.");
        }
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion = Guid.NewGuid();
    }
}
