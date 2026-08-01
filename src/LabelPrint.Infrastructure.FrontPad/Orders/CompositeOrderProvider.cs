using LabelPrint.Plugins.Abstractions.Orders;

namespace LabelPrint.Infrastructure.FrontPad.Orders;

/// <summary>
/// Combines dev file inbox with FrontPad API placeholder (no fake cloud sync).
/// </summary>
public sealed class CompositeOrderProvider : IOrderProvider, IOrderProviderStatus
{
    private readonly JsonFileOrderProvider _fileProvider;
    private readonly NullOrderProvider _nullProvider;

    public CompositeOrderProvider(JsonFileOrderProvider fileProvider, NullOrderProvider nullProvider)
    {
        _fileProvider = fileProvider;
        _nullProvider = nullProvider;
    }

    /// <inheritdoc />
    public string ProviderKey => "composite";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalOrderDto>> GetNewOrdersAsync(CancellationToken cancellationToken = default)
    {
        // Dev inbox always available; live FrontPad returns empty until spike.
        var fileOrders = await _fileProvider.GetNewOrdersAsync(cancellationToken);
        _ = await _nullProvider.GetNewOrdersAsync(cancellationToken);
        return fileOrders;
    }

    /// <inheritdoc />
    public Task AcknowledgeAsync(string externalOrderId, CancellationToken cancellationToken = default) =>
        _fileProvider.AcknowledgeAsync(externalOrderId, cancellationToken);

    /// <inheritdoc />
    public string GetStatusMessage() => _nullProvider.GetStatusMessage();
}
