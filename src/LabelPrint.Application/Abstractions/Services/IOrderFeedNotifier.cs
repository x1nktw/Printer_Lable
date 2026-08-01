namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Signals that kitchen orders were imported (webhook / inbox poll) so the UI can refresh.
/// </summary>
public interface IOrderFeedNotifier
{
    event EventHandler? OrdersChanged;

    void NotifyOrdersChanged();
}
