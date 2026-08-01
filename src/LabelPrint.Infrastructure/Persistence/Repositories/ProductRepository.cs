using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LabelPrint.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly LabelPrintDbContext _db;

    public ProductRepository(LabelPrintDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Products
            .Include(p => p.CustomFields)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode, cancellationToken);

    public Task<Product?> GetByNameExactAsync(string name, CancellationToken cancellationToken = default) =>
        _db.Products.FirstOrDefaultAsync(
            p => p.Name.ToLower() == name.ToLower() && !p.IsArchived,
            cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        _db.Products.AnyAsync(p => p.Sku == sku && p.Id != excludeId, cancellationToken);

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        _db.Products.AnyAsync(p => p.Barcode == barcode && p.Id != excludeId, cancellationToken);

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? search,
        Guid? categoryId,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default,
        Guid? excludeCategoryId = null)
    {
        var query = _db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        if (categoryId is not null)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }
        else if (excludeCategoryId is not null)
        {
            query = query.Where(p => p.CategoryId != excludeCategoryId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.Sku, $"%{term}%") ||
                (p.Barcode != null && EF.Functions.Like(p.Barcode, $"%{term}%")));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await _db.Products.AddAsync(product, cancellationToken);

    public void Update(Product product) => _db.Products.Update(product);

    public async Task SoftArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return;
        }

        product.IsArchived = true;
        product.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task<IReadOnlySet<Guid>> GetReferencedTemplateIdsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = await _db.Products.AsNoTracking()
            .Where(p => !p.IsArchived && p.DefaultTemplateId != null)
            .Select(p => p.DefaultTemplateId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var orderItem = await _db.Products.AsNoTracking()
            .Where(p => !p.IsArchived && p.OrderItemTemplateId != null)
            .Select(p => p.OrderItemTemplateId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return defaults.Concat(orderItem).ToHashSet();
    }
}
