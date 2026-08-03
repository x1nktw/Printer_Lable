using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Catalog product application service.
/// </summary>
public interface IProductService
{
    /// <summary>Creates a new product.</summary>
    Task<Result<Guid>> CreateAsync(ProductUpsertDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing product.</summary>
    Task<Result> UpdateAsync(Guid id, ProductUpsertDto dto, CancellationToken cancellationToken = default);

    /// <summary>Soft-archives a product.</summary>
    Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a product by id.</summary>
    Task<Result<ProductUpsertDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Searches products with offset pagination for catalog grids.</summary>
    Task<Result<(IReadOnlyList<ProductListItemDto> Items, int TotalCount)>> SearchAsync(
        string? search,
        Guid? categoryId,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default,
        Guid? excludeCategoryId = null,
        IReadOnlyCollection<Guid>? categoryIds = null,
        IReadOnlyCollection<Guid>? excludeCategoryIds = null);
}
