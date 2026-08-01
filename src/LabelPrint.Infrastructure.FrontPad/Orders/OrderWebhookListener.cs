using System.Net;
using System.Text;
using System.Text.Json;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Infrastructure.FrontPad.Orders;

/// <summary>
/// Local HTTP listener for LabelPrint FrontPad Bridge (full order JSON with items).
/// Official FrontPad change_status webhooks are ignored — we do not use that API surface.
/// </summary>
public sealed class OrderWebhookListener : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOrderFeedNotifier _feedNotifier;
    private readonly ILogger<OrderWebhookListener> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public OrderWebhookListener(
        IServiceScopeFactory scopeFactory,
        IOrderFeedNotifier feedNotifier,
        ILogger<OrderWebhookListener> logger)
    {
        _scopeFactory = scopeFactory;
        _feedNotifier = feedNotifier;
        _logger = logger;
    }

    public void Start(string? listenUrl)
    {
        if (string.IsNullOrWhiteSpace(listenUrl))
        {
            return;
        }

        Stop();

        try
        {
            _listener = new HttpListener();
            var prefix = listenUrl.EndsWith('/') ? listenUrl : listenUrl + "/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            _logger.LogInformation("Order webhook listening at {Url}", prefix);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start webhook listener at {Url}", listenUrl);
            Stop();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_listener?.IsListening == true)
        {
            _listener.Stop();
        }

        _listener?.Close();
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                await HandleRequestAsync(context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook listener error");
                if (context is not null)
                {
                    try
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            context.Response.Close();
            return;
        }

        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            context.Response.StatusCode = 400;
            var msg = Encoding.UTF8.GetBytes("Empty body");
            await context.Response.OutputStream.WriteAsync(msg, cancellationToken);
            context.Response.Close();
            return;
        }

        // Official FrontPad status-only webhooks — not used.
        if (IsOfficialStatusOnlyWebhook(body))
        {
            _logger.LogDebug("Ignored official FrontPad status webhook (not used)");
            context.Response.StatusCode = 204;
            context.Response.Close();
            return;
        }

        OrderInboxPaths.EnsureDirectories();
        var fileName = $"webhook_{DateTime.UtcNow:yyyyMMddHHmmssfff}.json";
        var path = Path.Combine(OrderInboxPaths.InboxDirectory, fileName);
        await File.WriteAllTextAsync(path, body, Encoding.UTF8, cancellationToken);

        context.Response.StatusCode = 202;
        var accepted = Encoding.UTF8.GetBytes("Accepted");
        await context.Response.OutputStream.WriteAsync(accepted, cancellationToken);
        context.Response.Close();
        _logger.LogInformation("Webhook payload saved to inbox: {File}", fileName);

        await ImportInboxAndNotifyAsync(cancellationToken);
    }

    private async Task ImportInboxAndNotifyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var settings = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Settings.GetAsync(cancellationToken);
            var sync = await orders.SyncInboxOrdersAsync(cancellationToken);
            if (sync.IsFailure)
            {
                _logger.LogWarning("Immediate inbox import failed: {Error}", sync.Error);
                _feedNotifier.NotifyOrdersChanged();
                return;
            }

            if (sync.Value.NewOrderIds.Count > 0)
            {
                _logger.LogInformation("Webhook imported {Count} order(s) immediately", sync.Value.NewOrderIds.Count);
                if (settings.AutoPrintOrders)
                {
                    foreach (var id in sync.Value.NewOrderIds)
                    {
                        var print = await orders.PrintAllItemsAsync(id, cancellationToken: cancellationToken);
                        if (print.IsFailure)
                        {
                            _logger.LogWarning("Auto-print failed for {OrderId}: {Error}", id, print.Error);
                        }
                    }
                }
            }

            _feedNotifier.NotifyOrdersChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Immediate webhook import failed");
            _feedNotifier.NotifyOrdersChanged();
        }
    }

    private static bool IsOfficialStatusOnlyWebhook(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("action", out var actionEl))
            {
                return false;
            }

            return string.Equals(actionEl.GetString(), "change_status", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
