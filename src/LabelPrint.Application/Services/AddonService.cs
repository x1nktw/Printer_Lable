using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Catalog CRUD for add-on icon mappings.
/// </summary>
public sealed class AddonService : IAddonService
{
    public static readonly IReadOnlyList<string> DefaultBuiltInIconKeys =
        new[] { "pepper", "cheese", "onion", "bullet" };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAddonIconResolver _iconResolver;
    private readonly ILogger<AddonService> _logger;

    public AddonService(
        IUnitOfWork unitOfWork,
        IAddonIconResolver iconResolver,
        ILogger<AddonService> logger)
    {
        _unitOfWork = unitOfWork;
        _iconResolver = iconResolver;
        _logger = logger;
    }

    public IReadOnlyList<string> BuiltInIconKeys => DefaultBuiltInIconKeys;

    public async Task<Result<IReadOnlyList<AddonListItemDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.Addons.ListAsync(includeArchived: false, cancellationToken);
        var dtos = items
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new AddonListItemDto
            {
                Id = a.Id,
                Name = a.Name,
                MatchAliases = a.MatchAliases,
                IconKey = a.IconKey
            })
            .ToList();
        return Result<IReadOnlyList<AddonListItemDto>>.Success(dtos);
    }

    public async Task<Result<Guid>> CreateAsync(AddonUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var error = Validate(dto);
        if (error is not null)
        {
            return Result<Guid>.Failure(error);
        }

        var addon = new Addon
        {
            Name = dto.Name.Trim(),
            MatchAliases = NormalizeAliases(dto.MatchAliases),
            IconKey = dto.IconKey.Trim()
        };
        await _unitOfWork.Addons.AddAsync(addon, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _iconResolver.InvalidateCache();
        _logger.LogInformation("Created addon mapping {AddonId} «{Name}» → {IconKey}", addon.Id, addon.Name, addon.IconKey);
        return Result<Guid>.Success(addon.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, AddonUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var error = Validate(dto);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        var addon = await _unitOfWork.Addons.GetByIdAsync(id, cancellationToken);
        if (addon is null || addon.IsArchived)
        {
            return Result.Failure("Добавка не найдена.");
        }

        addon.Name = dto.Name.Trim();
        addon.MatchAliases = NormalizeAliases(dto.MatchAliases);
        addon.IconKey = dto.IconKey.Trim();
        addon.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Addons.Update(addon);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _iconResolver.InvalidateCache();
        return Result.Success();
    }

    public async Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var addon = await _unitOfWork.Addons.GetByIdAsync(id, cancellationToken);
        if (addon is null)
        {
            return Result.Failure("Добавка не найдена.");
        }

        addon.IsArchived = true;
        addon.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Addons.Update(addon);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _iconResolver.InvalidateCache();
        return Result.Success();
    }

    private static string? Validate(AddonUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return "Укажите название добавки.";
        }

        if (string.IsNullOrWhiteSpace(dto.IconKey))
        {
            return "Выберите иконку.";
        }

        return null;
    }

    private static string? NormalizeAliases(string? aliases)
    {
        if (string.IsNullOrWhiteSpace(aliases))
        {
            return null;
        }

        var parts = aliases
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }
}
