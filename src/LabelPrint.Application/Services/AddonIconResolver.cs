using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Services;

/// <summary>
/// Resolves FrontPad add-on text to an icon key using the user catalog only.
/// </summary>
public sealed class AddonIconResolver : IAddonIconResolver
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly object _gate = new();
    private IReadOnlyList<Addon>? _cache;

    public AddonIconResolver(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public void InvalidateCache()
    {
        lock (_gate)
        {
            _cache = null;
        }
    }

    public async Task<string> ResolveIconKeyAsync(string addonText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(addonText))
        {
            return string.Empty;
        }

        var addons = await GetCachedAsync(cancellationToken);
        var text = addonText.Trim();
        var best = FindBestMatch(addons, text);
        if (best is null || string.IsNullOrWhiteSpace(best.IconKey))
        {
            return string.Empty;
        }

        return best.IconKey;
    }

    private async Task<IReadOnlyList<Addon>> GetCachedAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cache is not null)
            {
                return _cache;
            }
        }

        var list = await _unitOfWork.Addons.ListAsync(includeArchived: false, cancellationToken);
        lock (_gate)
        {
            _cache = list;
            return _cache;
        }
    }

    private static Addon? FindBestMatch(IReadOnlyList<Addon> addons, string addonText)
    {
        Addon? best = null;
        var bestLen = 0;
        foreach (var addon in addons)
        {
            foreach (var token in EnumerateMatchTokens(addon))
            {
                if (token.Length < 2)
                {
                    continue;
                }

                if (addonText.Contains(token, StringComparison.OrdinalIgnoreCase) && token.Length > bestLen)
                {
                    best = addon;
                    bestLen = token.Length;
                }
            }
        }

        return best;
    }

    private static IEnumerable<string> EnumerateMatchTokens(Addon addon)
    {
        yield return addon.Name;
        if (string.IsNullOrWhiteSpace(addon.MatchAliases))
        {
            yield break;
        }

        foreach (var part in addon.MatchAliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }
}
