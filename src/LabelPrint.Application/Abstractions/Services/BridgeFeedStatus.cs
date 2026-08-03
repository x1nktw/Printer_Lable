namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Live Bridge → webhook feed state for the Home system status panel.
/// </summary>
public sealed class BridgeFeedStatus
{
    /// <summary>Heartbeat every ~60s from Bridge; allow 3 missed beats.</summary>
    public static readonly TimeSpan BridgeOnlineWindow = TimeSpan.FromMinutes(3);

    /// <summary>FrontPad tab hook considered fresh (content script keepalive ~20s).</summary>
    public static readonly TimeSpan FrontPadOnlineWindow = TimeSpan.FromSeconds(60);

    /// <summary>Local HTTP listener is accepting requests.</summary>
    public bool IsListening { get; init; }

    /// <summary>Last Bridge heartbeat or order POST (UTC).</summary>
    public DateTimeOffset? LastSeenAt { get; init; }

    /// <summary>Bridge toggle from last heartbeat; null if never seen.</summary>
    public bool? BridgeEnabled { get; init; }

    /// <summary>FrontPad page hook was recently active per last heartbeat.</summary>
    public bool FrontPadHookActive { get; init; }

    /// <summary>Last FrontPad hook timestamp reported by Bridge (UTC).</summary>
    public DateTimeOffset? FrontPadHookSeenAt { get; init; }

    /// <summary>True when a Bridge heartbeat arrived recently.</summary>
    public bool IsBridgeOnline =>
        LastSeenAt is { } seen && DateTimeOffset.UtcNow - seen <= BridgeOnlineWindow;
}
