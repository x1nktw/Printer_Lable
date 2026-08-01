namespace LabelPrint.Application.Abstractions;

/// <summary>
/// Abstraction over the current UTC time for testability.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC timestamp.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>System clock implementation.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
