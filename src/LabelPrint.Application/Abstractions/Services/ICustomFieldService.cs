using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Custom product field definition management.
/// </summary>
public interface ICustomFieldService
{
    Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateAsync(
        string name,
        CustomFieldDataType dataType,
        bool isRequired,
        CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
}
