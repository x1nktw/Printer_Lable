using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Custom field definition persistence port.
/// </summary>
public interface ICustomFieldDefinitionRepository
{
    Task<CustomFieldDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default);

    void Update(CustomFieldDefinition definition);
}
