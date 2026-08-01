using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Services;

/// <inheritdoc />
public sealed class LabelDateTimeService : ILabelDateTimeService
{
    private readonly IUnitOfWork _unitOfWork;

    public LabelDateTimeService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    /// <inheritdoc />
    public async Task<DateTimeOffset> GetEffectiveAsync(
        DateTimeOffset? printOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (printOverride is not null)
        {
            return printOverride.Value;
        }

        var settings = await _unitOfWork.Settings.GetAsync(cancellationToken);
        if (settings.LabelDateTimeMode == LabelDateTimeMode.Manual
            && settings.ManualLabelDateTime is { } manual)
        {
            return manual;
        }

        return DateTimeOffset.Now;
    }
}
