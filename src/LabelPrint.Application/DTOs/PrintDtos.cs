using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.DTOs;

/// <summary>Print queue list row.</summary>
public sealed class PrintQueueItemDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public PrintJobStatus Status { get; init; }

    public Guid PrinterId { get; init; }

    public string PrinterName { get; init; } = string.Empty;

    public int Copies { get; init; }

    public int RetryCount { get; init; }

    public string? FailureReason { get; init; }

    public bool IsTransientFailure { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool CanCancel { get; init; }

    public bool CanRetry { get; init; }
}

/// <summary>Print history list row.</summary>
public sealed class PrintHistoryItemDto
{
    public Guid Id { get; init; }

    public DateTimeOffset PrintedAt { get; init; }

    public PrintJobStatus Status { get; init; }

    public string Description { get; init; } = string.Empty;

    public string? PrinterName { get; init; }

    public string? ProductName { get; init; }

    public int Copies { get; init; }

    public string? FailureReason { get; init; }
}
