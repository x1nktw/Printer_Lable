using LabelPrint.Application.Common;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Product catalog CSV import and export.
/// </summary>
public interface IProductCsvService
{
    Task<Result<string>> ExportAsync(CancellationToken cancellationToken = default);

    Task<Result<int>> ImportAsync(string csv, CancellationToken cancellationToken = default);
}
