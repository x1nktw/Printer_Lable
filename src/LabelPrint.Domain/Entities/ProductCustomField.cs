namespace LabelPrint.Domain.Entities;

/// <summary>
/// EAV value of a custom field for a product.
/// </summary>
public class ProductCustomField
{
    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    public Guid FieldDefinitionId { get; set; }

    public CustomFieldDefinition? FieldDefinition { get; set; }

    public string? Value { get; set; }
}
