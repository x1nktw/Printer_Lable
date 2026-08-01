using System.Reflection;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;

namespace LabelPrint.Infrastructure.Services;

/// <summary>
/// Stub update checker until a release feed URL is configured.
/// </summary>
public sealed class StubUpdateChecker : IUpdateChecker
{
    /// <inheritdoc />
    public Task<Result<UpdateCheckResult>> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        var current = version is null ? "0.8.0" : $"{version.Major}.{version.Minor}.{version.Build}";

        return Task.FromResult(Result.Success(new UpdateCheckResult
        {
            CurrentVersion = current,
            LatestVersion = null,
            Message = "Updates not configured. Configure a release feed in a future version.",
            UpdateAvailable = false
        }));
    }
}
