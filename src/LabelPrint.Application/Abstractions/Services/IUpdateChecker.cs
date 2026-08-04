using LabelPrint.Application.Common;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Checks for application updates and applies them (Velopack).
/// </summary>
public interface IUpdateChecker
{
    /// <summary>Returns the installed version and whether a newer release is available.</summary>
    Task<Result<UpdateCheckResult>> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the available update and applies it, restarting the app.
    /// Does not return on success (process is restarted).
    /// </summary>
    Task<Result> DownloadAndApplyAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of an update check.</summary>
public sealed class UpdateCheckResult
{
    /// <summary>Currently installed application version.</summary>
    public string CurrentVersion { get; init; } = "0.0.0";

    /// <summary>Latest published version when known.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Human-readable status.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>True when a newer version is available.</summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>True when this build was installed via Velopack (in-app updates work).</summary>
    public bool IsVelopackInstall { get; init; }

    /// <summary>Browser URL of the GitHub releases page.</summary>
    public string? ReleasePageUrl { get; init; }
}
