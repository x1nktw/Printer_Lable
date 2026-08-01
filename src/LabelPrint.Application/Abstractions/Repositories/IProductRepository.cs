using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Product catalog persistence port.
/// </summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    Task<Product?> GetByNameExactAsync(string name, CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? search,
        Guid? categoryId,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    void Update(Product product);

    Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default);
}
