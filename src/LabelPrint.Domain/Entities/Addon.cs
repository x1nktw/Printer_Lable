using LabelPrint.Domain.Common;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Catalog entry that maps FrontPad / kitchen add-on text to a label icon.
/// </summary>
public class Addon : EntityBase
{
    /// <summary>Display name and primary match text (case-insensitive contains).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Extra match substrings, comma-separated.</summary>
    public string? MatchAliases { get; set; }

    /// <summary>
    /// Custom file stem under the addon-icons folder (no built-in product icons).
    /// </summary>
    public string IconKey { get; set; } = string.Empty;

    public bool IsArchived { get; set; }
}
