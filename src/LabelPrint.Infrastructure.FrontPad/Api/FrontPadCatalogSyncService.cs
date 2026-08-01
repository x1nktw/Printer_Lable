using System.Text.Json;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Infrastructure.FrontPad.Api;

/// <summary>
/// Upserts local catalog from FrontPad get_products (SKU = артикул).
/// </summary>
public sealed class FrontPadCatalogSyncService : IFrontPadCatalogSyncService
{
    private static readonly string SyncStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LabelPrintPro",
        "frontpad-product-sync.json");

    private readonly IFrontPadApiClient _api;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FrontPadCatalogSyncService> _logger;

    public FrontPadCatalogSyncService(
        IFrontPadApiClient api,
        IUnitOfWork unitOfWork,
        ILogger<FrontPadCatalogSyncService> logger)
    {
        _api = api;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<FrontPadCatalogSyncResult>> SyncProductsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSyncProducts(out var waitMessage))
        {
            return Result.Failure<FrontPadCatalogSyncResult>(waitMessage);
        }

        var products = await _api.GetProductsAsync(cancellationToken);
        if (products.IsFailure)
        {
            return Result.Failure<FrontPadCatalogSyncResult>(products.Error!);
        }

        var created = 0;
        var updated = 0;
        foreach (var row in products.Value)
        {
            var existing = await _unitOfWork.Products.GetBySkuAsync(row.ProductId, cancellationToken);
            if (existing is null)
            {
                var product = new Product
                {
                    Name = row.Name,
                    Sku = row.ProductId
                };
                product.SetPrice(new Money(row.Price, "RUB"));
                await _unitOfWork.Products.AddAsync(product, cancellationToken);
                created++;
            }
            else
            {
                existing.Name = row.Name;
                existing.SetPrice(new Money(row.Price, "RUB"));
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                if (existing.IsArchived)
                {
                    existing.IsArchived = false;
                }

                _unitOfWork.Products.Update(existing);
                updated++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        MarkSynced();
        _logger.LogInformation(
            "FrontPad catalog sync: created {Created}, updated {Updated}, total {Total}",
            created, updated, products.Value.Count);

        return Result.Success(new FrontPadCatalogSyncResult
        {
            Created = created,
            Updated = updated,
            TotalFromApi = products.Value.Count,
            Message = $"Каталог FrontPad: +{created} / обновлено {updated} (всего {products.Value.Count})."
        });
    }

    private static bool CanSyncProducts(out string message)
    {
        message = string.Empty;
        try
        {
            if (!File.Exists(SyncStatePath))
            {
                return true;
            }

            var json = File.ReadAllText(SyncStatePath);
            var state = JsonSerializer.Deserialize<SyncState>(json);
            if (state?.LastProductsSyncUtc is null)
            {
                return true;
            }

            var elapsed = DateTimeOffset.UtcNow - state.LastProductsSyncUtc.Value;
            if (elapsed < TimeSpan.FromHours(1))
            {
                var left = TimeSpan.FromHours(1) - elapsed;
                message =
                    $"FrontPad ограничивает get_products до 1 раза в час. Повтор через {left.Minutes} мин. " +
                    "Заказы через API не отдаются — только webhook статуса или JSON-inbox.";
                return false;
            }
        }
        catch
        {
            return true;
        }

        return true;
    }

    private static void MarkSynced()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SyncStatePath)!);
        var json = JsonSerializer.Serialize(new SyncState { LastProductsSyncUtc = DateTimeOffset.UtcNow });
        File.WriteAllText(SyncStatePath, json);
    }

    private sealed class SyncState
    {
        public DateTimeOffset? LastProductsSyncUtc { get; set; }
    }
}
