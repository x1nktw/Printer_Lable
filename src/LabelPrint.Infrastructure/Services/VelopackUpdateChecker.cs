using System.Reflection;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velopack;
using Velopack.Sources;
using AppUpdateOptions = LabelPrint.Application.Options.UpdateOptions;

namespace LabelPrint.Infrastructure.Services;

/// <summary>
/// Velopack updates fed from GitHub Releases (works for installed + portable Velopack packages).
/// </summary>
public sealed class VelopackUpdateChecker : IUpdateChecker
{
    private readonly AppUpdateOptions _options;
    private readonly ILogger<VelopackUpdateChecker> _logger;

    public VelopackUpdateChecker(
        IOptions<AppUpdateOptions> options,
        ILogger<VelopackUpdateChecker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<UpdateCheckResult>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();
        var releasePage = $"{_options.ResolveRepoUrl()}/releases";

        if (!_options.Enabled)
        {
            return Result.Success(new UpdateCheckResult
            {
                CurrentVersion = current,
                LatestVersion = current,
                Message = "Автообновление отключено в настройках.",
                UpdateAvailable = false,
                ReleasePageUrl = releasePage
            });
        }

        try
        {
            var mgr = CreateManager();
            if (!mgr.IsInstalled)
            {
                return Result.Success(new UpdateCheckResult
                {
                    CurrentVersion = current,
                    Message =
                        "In-app обновления работают после установки через LabelPrintPro Setup (Velopack). " +
                        "Скачайте установщик со страницы релизов.",
                    UpdateAvailable = false,
                    IsVelopackInstall = false,
                    ReleasePageUrl = releasePage
                });
            }

            cancellationToken.ThrowIfCancellationRequested();
            var update = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                return Result.Success(new UpdateCheckResult
                {
                    CurrentVersion = current,
                    LatestVersion = current,
                    Message = "Установлена актуальная версия.",
                    UpdateAvailable = false,
                    IsVelopackInstall = true,
                    ReleasePageUrl = releasePage
                });
            }

            var latest = update.TargetFullRelease.Version.ToString();
            return Result.Success(new UpdateCheckResult
            {
                CurrentVersion = current,
                LatestVersion = latest,
                Message = $"Доступна v{latest}. Нажмите «Обновить» — скачивание и перезапуск автоматически.",
                UpdateAvailable = true,
                IsVelopackInstall = true,
                ReleasePageUrl = releasePage
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Velopack update check failed");
            return Result.Failure<UpdateCheckResult>($"Ошибка проверки обновлений: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> DownloadAndApplyAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mgr = CreateManager();
            if (!mgr.IsInstalled)
            {
                return Result.Failure(
                    "Эта копия не установлена через Velopack. Установите LabelPrintPro Setup с GitHub Releases.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var update = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                return Result.Failure("Нет доступных обновлений.");
            }

            await mgr.DownloadUpdatesAsync(
                    update,
                    p => progress?.Report(Math.Clamp(p / 100.0, 0, 1)))
                .ConfigureAwait(false);

            progress?.Report(1);
            _logger.LogInformation(
                "Applying Velopack update to {Version}",
                update.TargetFullRelease.Version);

            // Does not return — process is restarted.
            mgr.ApplyUpdatesAndRestart(update);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Velopack download/apply failed");
            return Result.Failure($"Не удалось обновить: {ex.Message}");
        }
    }

    private UpdateManager CreateManager()
    {
        var source = new GithubSource(
            _options.ResolveRepoUrl(),
            _options.GitHubToken,
            _options.IncludePrerelease);
        return new UpdateManager(source);
    }

    private string GetCurrentVersion()
    {
        try
        {
            var mgr = CreateManager();
            if (mgr.CurrentVersion is { } installed)
            {
                return installed.ToString();
            }
        }
        catch
        {
            // not a Velopack install / locator unavailable
        }

        var version = Assembly.GetEntryAssembly()?.GetName().Version
                      ?? Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null)
        {
            return "0.9.0";
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
