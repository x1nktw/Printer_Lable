using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Processes claimed print jobs (render + gateway dispatch) with retry handling.
/// </summary>
public interface IPrintJobProcessor
{
    /// <summary>
    /// Loads dependencies and runs the print pipeline for a claimed pending job.
    /// Handles transient failures with automatic requeue up to <paramref name="maxRetries"/>.
    /// </summary>
    Task ProcessClaimedJobAsync(PrintJob job, int maxRetries, CancellationToken cancellationToken = default);
}
