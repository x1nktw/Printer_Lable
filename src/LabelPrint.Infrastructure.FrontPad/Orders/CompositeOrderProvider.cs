using LabelPrint.Plugins.Abstractions.Orders;

namespace LabelPrint.Infrastructure.FrontPad.Orders;

/// <summary>
/// Order source: local JSON inbox (filled by Bridge webhook). No shop API pull.
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
    public Task<IReadOnlyList<ExternalOrderDto>> GetNewOrdersAsync(CancellationToken cancellationToken = default) =>
        _fileProvider.GetNewOrdersAsync(cancellationToken);

    /// <inheritdoc />
    public Task AcknowledgeAsync(string externalOrderId, CancellationToken cancellationToken = default) =>
        _fileProvider.AcknowledgeAsync(externalOrderId, cancellationToken);

    /// <inheritdoc />
    public string GetStatusMessage() => _nullProvider.GetStatusMessage();
}
