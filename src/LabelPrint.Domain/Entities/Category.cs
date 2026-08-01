using LabelPrint.Domain.Common;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Hierarchical product category.
/// </summary>
public class Category : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public Guid? ParentId { get; set; }

    public Category? Parent { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();

    /// <summary>Soft archive flag.</summary>
    public bool IsArchived { get; set; }
}
