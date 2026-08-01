using System.Globalization;
using System.Text;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Simple CSV import/export for the product catalog.
/// </summary>
public sealed class ProductCsvService : IProductCsvService
{
    private const string Header = "Name,Sku,Barcode,Price,CategoryName";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductService _productService;
    private readonly ILogger<ProductCsvService> _logger;

    public ProductCsvService(
        IUnitOfWork unitOfWork,
        IProductService productService,
        ILogger<ProductCsvService> logger)
    {
        _unitOfWork = unitOfWork;
        _productService = productService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> ExportAsync(CancellationToken cancellationToken = default)
    {
        var (items, _) = await _unitOfWork.Products.SearchAsync(
            search: null,
            categoryId: null,
            includeArchived: false,
            skip: 0,
            take: 10_000,
            cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var product in items.OrderBy(p => p.Name))
        {
            var categoryName = product.Category?.Name ?? string.Empty;
            sb.Append(EscapeField(product.Name)).Append(',');
            sb.Append(EscapeField(product.Sku)).Append(',');
            sb.Append(EscapeField(product.Barcode ?? string.Empty)).Append(',');
            sb.Append(product.PriceAmount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(EscapeField(categoryName));
        }

        return Result.Success(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<Result<int>> ImportAsync(string csv, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Result.Failure<int>("CSV content is empty.");
        }

        var rows = ParseCsv(csv);
        if (rows.Count == 0)
        {
            return Result.Failure<int>("CSV has no data rows.");
        }

        var categories = await _unitOfWork.Categories.GetAllAsync(includeArchived: false, cancellationToken);
        var categoryByName = categories.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var startIndex = 0;
        if (IsHeaderRow(rows[0]))
        {
            startIndex = 1;
        }

        var imported = 0;
        for (var i = startIndex; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (row.Length < 4)
            {
                return Result.Failure<int>($"Row {i + 1}: expected at least Name, Sku, Barcode, Price.");
            }

            var name = row[0].Trim();
            var sku = row[1].Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sku))
            {
                return Result.Failure<int>($"Row {i + 1}: Name and Sku are required.");
            }

            if (!decimal.TryParse(row[3].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                return Result.Failure<int>($"Row {i + 1}: invalid price '{row[3]}'.");
            }

            var barcode = row.Length > 2 && !string.IsNullOrWhiteSpace(row[2]) ? row[2].Trim() : null;
            var categoryName = row.Length > 4 ? row[4].Trim() : string.Empty;
            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                if (!categoryByName.TryGetValue(categoryName, out var category))
                {
                    return Result.Failure<int>($"Row {i + 1}: category '{categoryName}' not found.");
                }

                categoryId = category.Id;
            }

            var dto = new ProductUpsertDto
            {
                Name = name,
                Sku = sku,
                Barcode = barcode,
                PriceAmount = price,
                CategoryId = categoryId
            };

            var existing = await _unitOfWork.Products.GetBySkuAsync(sku, cancellationToken);
            Result result;
            if (existing is null || existing.IsArchived)
            {
                var created = await _productService.CreateAsync(dto, cancellationToken);
                result = created.IsSuccess ? Result.Success() : Result.Failure(created.Error!);
            }
            else
            {
                result = await _productService.UpdateAsync(existing.Id, dto, cancellationToken);
            }

            if (result.IsFailure)
            {
                return Result.Failure<int>($"Row {i + 1}: {result.Error}");
            }

            imported++;
        }

        _logger.LogInformation("Imported {Count} products from CSV", imported);
        return Result.Success(imported);
    }

    private static bool IsHeaderRow(string[] row) =>
        row.Length >= 2 &&
        row[0].Equals("Name", StringComparison.OrdinalIgnoreCase) &&
        row[1].Equals("Sku", StringComparison.OrdinalIgnoreCase);

    private static string EscapeField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private static List<string[]> ParseCsv(string csv)
    {
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow.ToArray());
                    currentRow = new List<string>();
                    break;
                default:
                    currentField.Append(c);
                    break;
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }
}
