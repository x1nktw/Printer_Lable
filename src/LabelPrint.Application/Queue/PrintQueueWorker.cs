using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelPrint.Application.Queue;

/// <summary>
/// Background loop that claims pending jobs per active printer and dispatches them.
/// </summary>
public sealed class PrintQueueWorker : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PrintQueueOptions _options;
    private readonly ILogger<PrintQueueWorker> _logger;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public PrintQueueWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PrintQueueOptions> options,
        ILogger<PrintQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsRunning => _workerTask is { IsCompleted: false };

    public void Start()
    {
        if (!_options.UseBackgroundWorker || IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(() => RunAsync(_cts.Token));
        _logger.LogInformation("Print queue worker started (poll {PollMs} ms)", _options.PollIntervalMs);
    }

    public async Task StopAsync()
    {
        if (_cts is null || _workerTask is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            await _workerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _workerTask = null;
            _logger.LogInformation("Print queue worker stopped");
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Print queue worker iteration failed");
            }

            try
            {
                await Task.Delay(_options.PollIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var processor = scope.ServiceProvider.GetRequiredService<IPrintJobProcessor>();

        var settings = await unitOfWork.Settings.GetAsync(cancellationToken);
        var maxRetries = settings.MaxPrintRetries;

        var printers = await unitOfWork.Printers.GetAllAsync(includeInactive: false, cancellationToken);
        foreach (var printer in printers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var job = await unitOfWork.PrintJobs.TryClaimNextAsync(printer.Id, Guid.Empty, cancellationToken);
            if (job is null)
            {
                continue;
            }

            if (job.RetryCount > 0)
            {
                var backoffMs = _options.RetryBackoffBaseMs * (1 << Math.Min(job.RetryCount - 1, 5));
                await Task.Delay(backoffMs, cancellationToken);
            }

            await processor.ProcessClaimedJobAsync(job, maxRetries, cancellationToken);
        }
    }
}
