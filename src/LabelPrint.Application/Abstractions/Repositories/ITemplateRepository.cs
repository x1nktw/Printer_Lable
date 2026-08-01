using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Label template persistence port.
/// </summary>
public interface ITemplateRepository
{
    Task<LabelTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LabelTemplate> Items, int TotalCount)> SearchAsync(
        string? search,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(LabelTemplate template, CancellationToken cancellationToken = default);

    void Update(LabelTemplate template);

    Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default);
}
