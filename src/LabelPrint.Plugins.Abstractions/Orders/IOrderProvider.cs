namespace LabelPrint.Plugins.Abstractions.Orders;

/// <summary>
/// Port for receiving orders from an external POS/CRM (FrontPad or plugins).
/// </summary>
public interface IOrderProvider
{
    /// <summary>Provider unique key, e.g. "frontpad".</summary>
    string ProviderKey { get; }

    /// <summary>Fetches newly available orders from the external system.</summary>
    Task<IReadOnlyList<ExternalOrderDto>> GetNewOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>Acknowledges that an external order was persisted locally (idempotency aid).</summary>
    Task AcknowledgeAsync(string externalOrderId, CancellationToken cancellationToken = default);
}

/// <summary>Anti-corruption DTO for an external order.</summary>
public sealed record ExternalOrderDto(
    string ExternalOrderId,
    string Number,
    string? CustomerName,
    string? CustomerPhone,
    string? Comment,
    string? Address,
    string? Employee,
    decimal? TotalAmount,
    DateTimeOffset? OrderedAt,
    string? StatusCode,
    string? RawPayloadJson,
    IReadOnlyList<ExternalOrderItemDto> Items);

/// <summary>Anti-corruption DTO for an external order line.</summary>
public sealed record ExternalOrderItemDto(
    string? ExternalProductId,
    string? Sku,
    string? Barcode,
    string Name,
    decimal Quantity,
    decimal? Price,
    string? Comment);
