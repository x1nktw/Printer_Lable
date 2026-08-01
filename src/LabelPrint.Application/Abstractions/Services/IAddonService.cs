using LabelPrint.Application.Common;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>Add-on catalog row for UI.</summary>
public sealed class AddonListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? MatchAliases { get; init; }
    public string IconKey { get; init; } = "bullet";
}

/// <summary>Create/update payload for an add-on icon mapping.</summary>
public sealed class AddonUpsertDto
{
    public string Name { get; set; } = string.Empty;
    public string? MatchAliases { get; set; }
    public string IconKey { get; set; } = "bullet";
}

/// <summary>
/// CRUD for kitchen add-on → icon mappings used on order labels.
/// </summary>
public interface IAddonService
{
    Task<Result<IReadOnlyList<AddonListItemDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateAsync(AddonUpsertDto dto, CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(Guid id, AddonUpsertDto dto, CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Built-in icon keys shipped with the app.</summary>
    IReadOnlyList<string> BuiltInIconKeys { get; }
}

/// <summary>
/// Resolves which icon key to draw for a free-text FrontPad add-on.
/// </summary>
public interface IAddonIconResolver
{
    Task<string> ResolveIconKeyAsync(string addonText, CancellationToken cancellationToken = default);

    /// <summary>Invalidates any in-memory cache after catalog edits.</summary>
    void InvalidateCache();
}
