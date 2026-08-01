using LabelPrint.Application.Common;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// FrontPad HTTP API (official internet-shop API).
/// </summary>
/// <remarks>
/// Docs: POST form to https://app.frontpad.ru/api/index.php?METHOD with secret.
/// LabelPrint uses only get_products (catalog). Orders come via FrontPad Bridge webhook.
/// </remarks>
public interface IFrontPadApiClient
{
    /// <summary>Calls get_products and returns catalog rows (артикул → name/price).</summary>
    Task<Result<IReadOnlyList<FrontPadProductDto>>> GetProductsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Product row from FrontPad get_products.</summary>
public sealed class FrontPadProductDto
{
    public string ProductId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

/// <summary>Syncs FrontPad catalog into local products by SKU = артикул.</summary>
public interface IFrontPadCatalogSyncService
{
    /// <summary>Pulls get_products (max ~1/hour per FrontPad rules) and upserts local catalog.</summary>
    Task<Result<FrontPadCatalogSyncResult>> SyncProductsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Result of FrontPad catalog sync.</summary>
public sealed class FrontPadCatalogSyncResult
{
    public int Created { get; init; }

    public int Updated { get; init; }

    public int TotalFromApi { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>Combined sync for Orders UI button.</summary>
public sealed class OrderSyncSummaryDto
{
    public int OrdersFromInbox { get; init; }

    public int NewOrdersCreated { get; init; }

    public IReadOnlyList<Guid> NewOrderIds { get; init; } = Array.Empty<Guid>();

    public int ProductsCreated { get; init; }

    public int ProductsUpdated { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>Line for manually creating a kitchen order.</summary>
public sealed class KitchenOrderLineDto
{
    public Guid? ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Sku { get; init; }

    public decimal Quantity { get; init; } = 1;

    public decimal? Price { get; init; }
}
