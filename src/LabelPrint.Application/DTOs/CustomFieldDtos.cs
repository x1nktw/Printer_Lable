using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.DTOs;

/// <summary>Custom field definition for catalog configuration.</summary>
public sealed class CustomFieldDefinitionDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public CustomFieldDataType DataType { get; init; }

    public bool IsRequired { get; init; }

    public int SortOrder { get; init; }
}
