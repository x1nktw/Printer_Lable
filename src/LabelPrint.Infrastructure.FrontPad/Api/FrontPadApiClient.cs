using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Infrastructure.FrontPad.Api;

/// <summary>
/// HTTP client for FrontPad public API (form POST + secret).
/// </summary>
/// <remarks>
/// Based on official docs: https://docs.google.com/document/d/1gs81CYvJ6FD9KOseL3GOcrcR2YnEvjQqJn9mJRRc5Yk
/// Rate limits: ≤30/min, ≤2/sec; get_products ≤1/hour.
/// </remarks>
public sealed class FrontPadApiClient : IFrontPadApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FrontPadApiClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastCallUtc = DateTimeOffset.MinValue;

    public FrontPadApiClient(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<FrontPadApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FrontPadProductDto>>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Settings.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.FrontPadSecret))
        {
            return Result.Failure<IReadOnlyList<FrontPadProductDto>>(
                "Укажите секрет FrontPad в Настройках (раздел «Общие» в FrontPad → секретный код).");
        }

        var baseUrl = string.IsNullOrWhiteSpace(settings.FrontPadBaseUrl)
            ? "https://app.frontpad.ru/api/index.php"
            : settings.FrontPadBaseUrl.Trim();

        var url = AppendMethod(baseUrl, "get_products");
        var payload = await PostFormAsync(url, new Dictionary<string, string>
        {
            ["secret"] = settings.FrontPadSecret!
        }, cancellationToken);

        if (payload.IsFailure)
        {
            return Result.Failure<IReadOnlyList<FrontPadProductDto>>(payload.Error!);
        }

        return ParseProducts(payload.Value);
    }

    private async Task<Result<string>> PostFormAsync(
        string url,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sinceLast = DateTimeOffset.UtcNow - _lastCallUtc;
            if (sinceLast < TimeSpan.FromMilliseconds(550))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(550) - sinceLast, cancellationToken);
            }

            var client = _httpClientFactory.CreateClient("FrontPad");
            using var content = new FormUrlEncodedContent(fields);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            using var response = await client.PostAsync(url, content, cancellationToken);
            _lastCallUtc = DateTimeOffset.UtcNow;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FrontPad HTTP {Status}: {Body}", (int)response.StatusCode, body);
                return Result.Failure<string>($"FrontPad HTTP {(int)response.StatusCode}: {Trim(body)}");
            }

            return Result.Success(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FrontPad request failed");
            return Result.Failure<string>($"Ошибка запроса FrontPad: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Result<IReadOnlyList<FrontPadProductDto>> ParseProducts(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var result = root.TryGetProperty("result", out var resultEl) ? resultEl.GetString() : null;
        if (string.Equals(result, "error", StringComparison.OrdinalIgnoreCase))
        {
            var error = root.TryGetProperty("error", out var errEl) ? errEl.GetString() : "unknown";
            return Result.Failure<IReadOnlyList<FrontPadProductDto>>(MapApiError(error));
        }

        if (!string.Equals(result, "success", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<IReadOnlyList<FrontPadProductDto>>($"Неожиданный ответ FrontPad: {Trim(json)}");
        }

        var ids = ReadStringMap(root, "product_id");
        var names = ReadStringMap(root, "name");
        var prices = ReadStringMap(root, "price");
        if (ids.Count == 0)
        {
            return Result.Failure<IReadOnlyList<FrontPadProductDto>>("FrontPad вернул пустой список товаров (нужны артикулы в карточках).");
        }

        var list = new List<FrontPadProductDto>();
        foreach (var key in ids.Keys.OrderBy(k => int.TryParse(k, out var n) ? n : int.MaxValue).ThenBy(k => k))
        {
            var id = ids[key];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            names.TryGetValue(key, out var name);
            prices.TryGetValue(key, out var priceRaw);
            _ = decimal.TryParse(priceRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
                || decimal.TryParse(priceRaw, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out price);

            list.Add(new FrontPadProductDto
            {
                ProductId = id.Trim(),
                Name = string.IsNullOrWhiteSpace(name) ? id.Trim() : name.Trim(),
                Price = price
            });
        }

        return Result.Success((IReadOnlyList<FrontPadProductDto>)list);
    }

    private static Dictionary<string, string> ReadStringMap(JsonElement root, string propertyName)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(propertyName, out var el))
        {
            return map;
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                map[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    _ => prop.Value.ToString()
                };
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in el.EnumerateArray())
            {
                map[i.ToString(CultureInfo.InvariantCulture)] = item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : item.GetRawText();
                i++;
            }
        }

        return map;
    }

    public static string MapApiError(string? error) => error switch
    {
        "invalid_secret" => "Неверный секрет FrontPad (invalid_secret).",
        "api_off" => "API FrontPad выключен в настройках программы (api_off).",
        "invalid_plant" => "На текущем тарифе FrontPad API недоступен (invalid_plant). Нужен Корпоративный/Профессиональный.",
        "requests_limit" => "Превышен лимит запросов FrontPad (≤30/мин, get_products ≤1/час).",
        "invalid_products" => "Нет товаров с артикулом для выгрузки (invalid_products).",
        "cash_close" => "Смена в FrontPad закрыта (cash_close).",
        _ => $"Ошибка FrontPad: {error ?? "unknown"}"
    };

    private static string AppendMethod(string baseUrl, string method)
    {
        if (baseUrl.Contains('?', StringComparison.Ordinal))
        {
            return $"{baseUrl}&{method}";
        }

        return $"{baseUrl}?{method}";
    }

    private static string Trim(string text) =>
        text.Length <= 300 ? text : text[..300] + "…";
}
