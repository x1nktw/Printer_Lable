using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Application settings singleton persistence port.
/// </summary>
public interface IAppSettingsRepository
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    void Update(AppSettings settings);
}
