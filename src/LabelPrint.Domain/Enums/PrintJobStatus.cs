namespace LabelPrint.Domain.Enums;

/// <summary>
/// Lifecycle status of a print queue job (aggregate state machine).
/// </summary>
public enum PrintJobStatus
{
    Pending = 0,
    Rendering = 1,
    Printing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
