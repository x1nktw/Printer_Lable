using LabelPrint.Application.Common;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Checks for application updates (stub until a release feed is configured).
/// </summary>
public interface IUpdateChecker
{
    /// <summary>Returns the installed version and update availability message.</summary>
    Task<Result<UpdateCheckResult>> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>Result of an update check.</summary>
public sealed class UpdateCheckResult
{
    /// <summary>Currently installed application version.</summary>
    public string CurrentVersion { get; init; } = "0.0.0";

    /// <summary>Latest published version when known; otherwise same as <see cref="CurrentVersion"/>.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Human-readable status (e.g. updates not configured).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>True when a newer version is available.</summary>
    public bool UpdateAvailable { get; init; }
}
