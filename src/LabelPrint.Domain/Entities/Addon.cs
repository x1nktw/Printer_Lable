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
    /// Built-in key (<c>pepper</c>, <c>cheese</c>, <c>onion</c>, <c>bullet</c>)
    /// or a custom file stem under the addon-icons folder.
    /// </summary>
    public string IconKey { get; set; } = "bullet";

    public bool IsArchived { get; set; }
}
