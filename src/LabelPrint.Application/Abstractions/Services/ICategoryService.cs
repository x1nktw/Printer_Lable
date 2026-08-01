using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Category application service.
/// </summary>
public interface ICategoryService
{
    Task<Result<Guid>> CreateAsync(string name, Guid? parentId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<Category>>> GetTreeAsync(CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> RenameAsync(Guid id, string name, CancellationToken cancellationToken = default);
}
