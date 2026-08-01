using LabelPrint.Plugins.Abstractions.Orders;

namespace LabelPrint.Infrastructure.FrontPad.Orders;

/// <summary>
/// Status helper: orders arrive via Bridge webhook / inbox, not shop API.
/// </summary>
public sealed class NullOrderProvider : IOrderProvider, IOrderProviderStatus
{
    /// <inheritdoc />
    public string ProviderKey => "frontpad";

    /// <inheritdoc />
    public Task<IReadOnlyList<ExternalOrderDto>> GetNewOrdersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((IReadOnlyList<ExternalOrderDto>)Array.Empty<ExternalOrderDto>());

    /// <inheritdoc />
    public Task AcknowledgeAsync(string externalOrderId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public string GetStatusMessage() =>
        "Заказы: FrontPad Bridge → webhook / JSON-inbox. Shop API не используется.";
}
