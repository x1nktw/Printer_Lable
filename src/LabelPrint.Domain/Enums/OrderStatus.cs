namespace LabelPrint.Domain.Enums;

/// <summary>Business status of an order.</summary>
public enum OrderStatus
{
    New = 0,
    Confirmed = 1,
    InProgress = 2,
    Ready = 3,
    Delivering = 4,
    Completed = 5,
    Cancelled = 6,
    Unknown = 99
}
