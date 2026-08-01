namespace LabelPrint.Domain.Common;

/// <summary>
/// Base type for all persisted domain entities.
/// </summary>
public abstract class EntityBase
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC last update timestamp.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
