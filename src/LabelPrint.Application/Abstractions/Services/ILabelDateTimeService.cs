using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Resolves the effective date/time stamped on labels (settings + optional print override).
/// </summary>
public interface ILabelDateTimeService
{
    /// <summary>
    /// Priority: <paramref name="printOverride"/> → Manual settings → now.
    /// </summary>
    Task<DateTimeOffset> GetEffectiveAsync(
        DateTimeOffset? printOverride = null,
        CancellationToken cancellationToken = default);

    string FormatDate(DateTimeOffset value) => value.ToLocalTime().ToString("dd.MM.yyyy");

    string FormatTime(DateTimeOffset value) => value.ToLocalTime().ToString("HH:mm");

    string FormatDateTime(DateTimeOffset value) =>
        $"{FormatDate(value)} {FormatTime(value)}";
}
