using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Queue;

/// <summary>
/// Polls kitchen order inbox on an interval from settings (AutoRefreshOrders).
/// Optionally auto-prints newly created orders (AutoPrintOrders).
/// </summary>
public sealed class KitchenOrderPollWorker : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KitchenOrderPollWorker> _logger;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public KitchenOrderPollWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<KitchenOrderPollWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsRunning => _workerTask is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(() => RunAsync(_cts.Token));
        _logger.LogInformation("Kitchen order poll worker started");
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
            // expected
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _workerTask = null;
            _logger.LogInformation("Kitchen order poll worker stopped");
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delaySeconds = 120;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var settings = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Settings.GetAsync(cancellationToken);
                delaySeconds = Math.Max(30, settings.OrdersRefreshIntervalSeconds);

                if (settings.AutoRefreshOrders)
                {
                    var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                    var sync = await orders.SyncInboxOrdersAsync(cancellationToken);
                    if (sync.IsSuccess && sync.Value.NewOrderIds.Count > 0)
                    {
                        _logger.LogInformation("Kitchen inbox: {Count} new orders", sync.Value.NewOrderIds.Count);
                        if (settings.AutoPrintOrders)
                        {
                            foreach (var orderId in sync.Value.NewOrderIds)
                            {
                                var print = await orders.PrintAllItemsAsync(
                                    orderId,
                                    printerId: settings.OrdersPrintPrinterId,
                                    templateId: settings.OrdersPrintTemplateId,
                                    cancellationToken: cancellationToken);
                                if (print.IsFailure)
                                {
                                    _logger.LogWarning("Auto-print failed for {OrderId}: {Error}", orderId, print.Error);
                                }
                            }
                        }

                        scope.ServiceProvider.GetRequiredService<IOrderFeedNotifier>().NotifyOrdersChanged();
                    }
                    else if (sync.IsSuccess)
                    {
                        // still nudge UI periodically in case statuses changed elsewhere
                    }
                    else if (sync.IsFailure)
                    {
                        _logger.LogWarning("Kitchen inbox sync: {Error}", sync.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitchen order poll iteration failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
