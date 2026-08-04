using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Application service for hierarchical categories.
/// </summary>
public sealed class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(IUnitOfWork unitOfWork, ILogger<CategoryService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Guid>("Category name is required.");
        }

        if (parentId is not null)
        {
            var parent = await _unitOfWork.Categories.GetByIdAsync(parentId.Value, cancellationToken);
            if (parent is null || parent.IsArchived)
            {
                return Result.Failure<Guid>("Parent category not found.");
            }
        }

        var category = new Category
        {
            Name = name.Trim(),
            ParentId = parentId
        };

        await _unitOfWork.Categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category {CategoryId} created", category.Id);
        return Result.Success(category.Id);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Category>>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var all = await _unitOfWork.Categories.GetAllAsync(includeArchived: false, cancellationToken);
        return Result.Success(all);
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure("Category not found.");
        }

        // Move products from this category to the parent (or uncategorized).
        var (products, _) = await _unitOfWork.Products.SearchAsync(
            search: null,
            categoryId: id,
            includeArchived: true,
            skip: 0,
            take: 10_000,
            cancellationToken);
        foreach (var product in products)
        {
            product.CategoryId = category.ParentId;
            product.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Products.Update(product);
        }

        await _unitOfWork.Categories.SoftArchiveAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Category {CategoryId} archived; reparented {Count} product(s) to {ParentId}",
            id,
            products.Count,
            category.ParentId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RenameAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Category name is required.");
        }

        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken);
        if (category is null || category.IsArchived)
        {
            return Result.Failure("Category not found.");
        }

        category.Name = name.Trim();
        category.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Category {CategoryId} renamed", id);
        return Result.Success();
    }
}
