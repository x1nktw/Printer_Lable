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
    /// <summary>Bridge is considered connected if a heartbeat arrived within this window.</summary>
    public static readonly TimeSpan BridgeOnlineWindow = BridgeFeedStatus.BridgeOnlineWindow;

    /// <summary>FrontPad hook is considered active within this window after last hookSeenAt.</summary>
    public static readonly TimeSpan FrontPadOnlineWindow = BridgeFeedStatus.FrontPadOnlineWindow;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOrderFeedNotifier _feedNotifier;
    private readonly ILogger<OrderWebhookListener> _logger;
    private readonly object _statusLock = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    private DateTimeOffset? _lastSeenAt;
    private bool? _bridgeEnabled;
    private DateTimeOffset? _frontPadHookSeenAt;

    public OrderWebhookListener(
        IServiceScopeFactory scopeFactory,
        IOrderFeedNotifier feedNotifier,
        ILogger<OrderWebhookListener> logger)
    {
        _scopeFactory = scopeFactory;
        _feedNotifier = feedNotifier;
        _logger = logger;
    }

    /// <summary>True when HttpListener is accepting requests.</summary>
    public bool IsListening => _listener?.IsListening == true;

    public BridgeFeedStatus GetFeedStatus()
    {
        lock (_statusLock)
        {
            var now = DateTimeOffset.UtcNow;
            var hookActive = _frontPadHookSeenAt is { } hookAt
                             && now - hookAt <= FrontPadOnlineWindow;

            return new BridgeFeedStatus
            {
                IsListening = IsListening,
                LastSeenAt = _lastSeenAt,
                BridgeEnabled = _bridgeEnabled,
                FrontPadHookActive = hookActive,
                FrontPadHookSeenAt = _frontPadHookSeenAt
            };
        }
    }

    public void Start(string? listenUrl)
    {
        if (string.IsNullOrWhiteSpace(listenUrl))
        {
            return;
        }

        Stop();

        var prefixes = ExpandListenPrefixes(listenUrl);
        if (TryStartWithPrefixes(prefixes, out var multiError))
        {
            return;
        }

        // Dual 127.0.0.1+localhost often fails Windows URL ACL — fall back to primary only.
        var primary = new[] { prefixes[0] };
        if (TryStartWithPrefixes(primary, out var primaryError))
        {
            _logger.LogWarning(
                multiError,
                "Webhook listening only on {Url}; alternate prefix failed",
                primary[0]);
            return;
        }

        _logger.LogWarning(
            primaryError ?? multiError,
            "Could not start webhook listener at {Url}",
            listenUrl);
        Stop();
    }

    private bool TryStartWithPrefixes(IReadOnlyList<string> prefixes, out Exception? error)
    {
        error = null;
        try
        {
            Stop();
            _listener = new HttpListener();
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }

            _listener.Start();
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            _logger.LogInformation(
                "Order webhook listening at {Urls}",
                string.Join(", ", _listener.Prefixes));
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            try
            {
                _listener?.Close();
            }
            catch
            {
                // ignore
            }

            _listener = null;
            return false;
        }
    }

    /// <summary>
    /// Registers both 127.0.0.1 and localhost so Bridge URL mismatches still hit the listener.
    /// </summary>
    internal static IReadOnlyList<string> ExpandListenPrefixes(string listenUrl)
    {
        var normalized = listenUrl.EndsWith('/') ? listenUrl : listenUrl + "/";
        var prefixes = new List<string> { normalized };

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            if (string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                var alt = new UriBuilder(uri) { Host = "localhost" }.Uri.ToString();
                if (!alt.EndsWith('/'))
                {
                    alt += "/";
                }

                prefixes.Add(alt);
            }
            else if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                var alt = new UriBuilder(uri) { Host = "127.0.0.1" }.Uri.ToString();
                if (!alt.EndsWith('/'))
                {
                    alt += "/";
                }

                prefixes.Add(alt);
            }
        }

        return prefixes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
        if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            if (TryHandleHeartbeatQuery(context.Request.Url))
            {
                await WriteJsonAsync(context, 200, """{"ok":true,"type":"bridge-heartbeat"}""", cancellationToken);
                return;
            }

            await WriteJsonAsync(context, 200, JsonSerializer.Serialize(new
            {
                ok = true,
                listening = true,
                service = "LabelPrintPro",
                bridgeOnline = GetFeedStatus().IsBridgeOnline
            }), cancellationToken);
            return;
        }

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

        if (TryHandleHeartbeat(body))
        {
            await WriteJsonAsync(context, 200, """{"ok":true,"type":"bridge-heartbeat"}""", cancellationToken);
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

        NoteBridgeActivity(enabled: true);

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

    private bool TryHandleHeartbeatQuery(Uri? url)
    {
        if (url is null || string.IsNullOrEmpty(url.Query))
        {
            return false;
        }

        var args = ParseQuery(url.Query);
        if (!args.TryGetValue("bridge", out var bridge)
            || (!string.Equals(bridge, "1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(bridge, "heartbeat", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var enabled = true;
        if (args.TryGetValue("enabled", out var enabledRaw))
        {
            enabled = enabledRaw is "1" or "true" or "yes";
        }

        DateTimeOffset? hookSeenAt = null;
        if (args.TryGetValue("hookSeenAt", out var hookRaw)
            && !string.IsNullOrWhiteSpace(hookRaw)
            && DateTimeOffset.TryParse(Uri.UnescapeDataString(hookRaw), out var parsedHook))
        {
            hookSeenAt = parsedHook.ToUniversalTime();
        }

        var frontPadActive = args.TryGetValue("frontPad", out var fp)
                             && fp is "1" or "true" or "yes";

        ApplyHeartbeat(enabled, hookSeenAt, frontPadActive);
        return true;
    }

    private bool TryHandleHeartbeat(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl))
            {
                return false;
            }

            if (!string.Equals(typeEl.GetString(), "bridge-heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var enabled = true;
            if (root.TryGetProperty("enabled", out var enabledEl)
                && (enabledEl.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                enabled = enabledEl.GetBoolean();
            }

            DateTimeOffset? hookSeenAt = null;
            if (root.TryGetProperty("hookSeenAt", out var hookEl)
                && hookEl.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(hookEl.GetString(), out var parsedHook))
            {
                hookSeenAt = parsedHook.ToUniversalTime();
            }

            var frontPadActive = root.TryGetProperty("frontPadHookActive", out var fpEl)
                                 && fpEl.ValueKind == JsonValueKind.True;

            ApplyHeartbeat(enabled, hookSeenAt, frontPadActive);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyHeartbeat(bool enabled, DateTimeOffset? hookSeenAt, bool frontPadActive)
    {
        lock (_statusLock)
        {
            _lastSeenAt = DateTimeOffset.UtcNow;
            _bridgeEnabled = enabled;

            // frontPadActive=false must clear immediately — do not keep a stale hookSeenAt.
            if (!frontPadActive)
            {
                _frontPadHookSeenAt = null;
            }
            else if (hookSeenAt is not null)
            {
                _frontPadHookSeenAt = hookSeenAt;
            }
            else
            {
                _frontPadHookSeenAt = DateTimeOffset.UtcNow;
            }
        }

        _logger.LogDebug(
            "Bridge heartbeat received (enabled={Enabled}, frontPad={FrontPad}, hook={Hook})",
            enabled,
            frontPadActive,
            hookSeenAt);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                result[Uri.UnescapeDataString(part)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(part[..eq]);
            var value = Uri.UnescapeDataString(part[(eq + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private void NoteBridgeActivity(bool enabled)
    {
        lock (_statusLock)
        {
            _lastSeenAt = DateTimeOffset.UtcNow;
            _bridgeEnabled = enabled;
        }
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
                        var print = await orders.PrintAllItemsAsync(
                            id,
                            printerId: settings.OrdersPrintPrinterId,
                            templateId: settings.OrdersPrintTemplateId,
                            cancellationToken: cancellationToken);
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

    private static async Task WriteJsonAsync(
        HttpListenerContext context,
        int statusCode,
        string json,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        context.Response.Close();
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
