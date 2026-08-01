using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Persistence for kitchen add-on icon mappings.
/// </summary>
public interface IAddonRepository
{
    Task<IReadOnlyList<Addon>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<Addon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Addon addon, CancellationToken cancellationToken = default);

    void Update(Addon addon);
}
