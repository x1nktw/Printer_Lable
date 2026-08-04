namespace LabelPrint.Application.Options;

/// <summary>Auto-update settings (Velopack + GitHub Releases).</summary>
public sealed class UpdateOptions
{
    public const string SectionName = "Updates";

    /// <summary>When false, checks report current version only.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>GitHub repository URL, e.g. https://github.com/owner/repo</summary>
    public string RepoUrl { get; set; } = "https://github.com/x1nktw/Printer_Lable";

    /// <summary>GitHub repository owner (legacy; used if RepoUrl empty).</summary>
    public string Owner { get; set; } = "x1nktw";

    /// <summary>GitHub repository name (legacy; used if RepoUrl empty).</summary>
    public string Repo { get; set; } = "Printer_Lable";

    /// <summary>Include prerelease GitHub releases.</summary>
    public bool IncludePrerelease { get; set; }

    /// <summary>Optional GitHub token for higher API rate limits.</summary>
    public string? GitHubToken { get; set; }

    public string ResolveRepoUrl()
    {
        if (!string.IsNullOrWhiteSpace(RepoUrl))
        {
            return RepoUrl.Trim().TrimEnd('/');
        }

        return $"https://github.com/{Owner}/{Repo}";
    }
}
