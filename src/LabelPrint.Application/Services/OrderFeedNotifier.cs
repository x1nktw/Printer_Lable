namespace LabelPrint.Application.Services;

/// <summary>
/// Process-wide kitchen order feed notifications.
/// </summary>
public sealed class OrderFeedNotifier : Abstractions.Services.IOrderFeedNotifier
{
    public event EventHandler? OrdersChanged;

    public void NotifyOrdersChanged() => OrdersChanged?.Invoke(this, EventArgs.Empty);
}
