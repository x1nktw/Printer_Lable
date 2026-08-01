using System.Text.Json;
using System.Text.Json.Serialization;
using LabelPrint.Plugins.Abstractions.Orders;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Infrastructure.FrontPad.Orders;

/// <summary>
/// Development order provider reading JSON files from the local inbox folder.
/// </summary>
/// <remarks>
/// Inbox schema (%LocalAppData%\LabelPrintPro\orders-inbox\*.json):
/// <code>
/// {
///   "externalOrderId": "fp-12345",
///   "number": "12345",
///   "customerName": "Иван",
///   "customerPhone": "+79001234567",
///   "comment": "Без лука",
///   "address": "ул. Пример, 1",
///   "employee": "Официант",
///   "totalAmount": 1500.00,
///   "orderedAt": "2026-08-01T10:00:00+05:00",
///   "statusCode": "new",
///   "items": [
///     {
///       "externalProductId": "p1",
///       "sku": "SKU-001",
///       "name": "Бургер",
///       "quantity": 2,
///       "price": 500,
///       "comment": null
///     }
///   ]
/// }
/// </code>
/// Property names are case-insensitive. Processed files move to orders-inbox/processed/.
/// </remarks>
public sealed class JsonFileOrderProvider : IOrderProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ILogger<JsonFileOrderProvider> _logger;
    private readonly Dictionary<string, string> _pendingAckPaths = new(StringComparer.OrdinalIgnoreCase);

    public JsonFileOrderProvider(ILogger<JsonFileOrderProvider> logger) => _logger = logger;

    /// <inheritdoc />
    public string ProviderKey => "dev-file-inbox";

    /// <inheritdoc />
    public Task<IReadOnlyList<ExternalOrderDto>> GetNewOrdersAsync(CancellationToken cancellationToken = default)
    {
        OrderInboxPaths.EnsureDirectories();
        _pendingAckPaths.Clear();

        var orders = new List<ExternalOrderDto>();
        foreach (var file in Directory.EnumerateFiles(OrderInboxPaths.InboxDirectory, "*.json"))
        {
            if (Path.GetFileName(file).StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(file);
                var document = JsonSerializer.Deserialize<InboxOrderDocument>(json, JsonOptions);
                if (document is null || string.IsNullOrWhiteSpace(document.ExternalOrderId))
                {
                    _logger.LogWarning("Skipping invalid inbox file {File}", file);
                    continue;
                }

                var dto = Map(document, json);
                orders.Add(dto);
                _pendingAckPaths[dto.ExternalOrderId] = file;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read inbox order file {File}", file);
            }
        }

        return Task.FromResult((IReadOnlyList<ExternalOrderDto>)orders);
    }

    /// <inheritdoc />
    public Task AcknowledgeAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        if (!_pendingAckPaths.TryGetValue(externalOrderId, out var sourcePath))
        {
            return Task.CompletedTask;
        }

        try
        {
            OrderInboxPaths.EnsureDirectories();
            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(OrderInboxPaths.ProcessedDirectory, $"{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}");
            File.Move(sourcePath, destPath, overwrite: true);
            _pendingAckPaths.Remove(externalOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move processed inbox file for order {ExternalOrderId}", externalOrderId);
        }

        return Task.CompletedTask;
    }

    private static ExternalOrderDto Map(InboxOrderDocument document, string rawJson)
    {
        var items = document.Items?
            .Select(i =>
            {
                var comment = i.Comment;
                if (string.IsNullOrWhiteSpace(comment) && i.Addons is { Count: > 0 })
                {
                    comment = string.Join("\n", i.Addons.Where(a => !string.IsNullOrWhiteSpace(a)));
                }

                return new ExternalOrderItemDto(
                    i.ExternalProductId,
                    i.Sku,
                    i.Barcode,
                    i.Name ?? string.Empty,
                    i.Quantity <= 0 ? 1 : i.Quantity,
                    i.Price,
                    comment);
            })
            .ToList()
            ?? new List<ExternalOrderItemDto>();

        return new ExternalOrderDto(
            document.ExternalOrderId!,
            document.Number ?? document.ExternalOrderId!,
            document.CustomerName,
            document.CustomerPhone,
            document.Comment,
            document.Address,
            document.Employee,
            document.TotalAmount,
            document.OrderedAt,
            document.StatusCode,
            rawJson,
            items);
    }

    private sealed class InboxOrderDocument
    {
        public string? ExternalOrderId { get; set; }

        public string? Number { get; set; }

        public string? CustomerName { get; set; }

        public string? CustomerPhone { get; set; }

        public string? Comment { get; set; }

        public string? Address { get; set; }

        public string? Employee { get; set; }

        public decimal? TotalAmount { get; set; }

        public DateTimeOffset? OrderedAt { get; set; }

        public string? StatusCode { get; set; }

        public List<InboxOrderItemDocument>? Items { get; set; }
    }

    private sealed class InboxOrderItemDocument
    {
        public string? ExternalProductId { get; set; }

        public string? Sku { get; set; }

        public string? Barcode { get; set; }

        public string? Name { get; set; }

        public decimal Quantity { get; set; } = 1;

        public decimal? Price { get; set; }

        public string? Comment { get; set; }

        [JsonPropertyName("addons")]
        public List<string>? Addons { get; set; }
    }
}
