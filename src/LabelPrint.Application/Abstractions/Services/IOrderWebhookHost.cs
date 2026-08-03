namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Controls the local order webhook listener (Bridge → LabelPrint).
/// </summary>
public interface IOrderWebhookHost
{
    /// <summary>True when the HTTP listener is accepting Bridge posts.</summary>
    bool IsListening { get; }

    /// <summary>Current Bridge / FrontPad feed snapshot for system status.</summary>
    BridgeFeedStatus GetFeedStatus();

    void Start();

    void Stop();
}
