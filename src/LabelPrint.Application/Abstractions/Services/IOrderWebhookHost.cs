namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Controls the local order webhook listener (Bridge → LabelPrint).
/// </summary>
public interface IOrderWebhookHost
{
    void Start();

    void Stop();
}
