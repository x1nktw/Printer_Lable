using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Definition of a user-defined catalog field (EAV schema).
/// </summary>
public class CustomFieldDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public CustomFieldDataType DataType { get; set; } = CustomFieldDataType.Text;

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<ProductCustomField> Values { get; set; } = new List<ProductCustomField>();
}
