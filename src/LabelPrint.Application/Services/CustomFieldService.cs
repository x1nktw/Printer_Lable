using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Application service for custom product field definitions.
/// </summary>
public sealed class CustomFieldService : ICustomFieldService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CustomFieldService> _logger;

    public CustomFieldService(IUnitOfWork unitOfWork, ILogger<CustomFieldService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await _unitOfWork.CustomFieldDefinitions.GetAllAsync(includeArchived: false, cancellationToken);
        var dtos = definitions.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<CustomFieldDefinitionDto>>(dtos);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(
        string name,
        CustomFieldDataType dataType,
        bool isRequired,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Guid>("Field name is required.");
        }

        var existing = await _unitOfWork.CustomFieldDefinitions.GetAllAsync(includeArchived: false, cancellationToken);
        if (existing.Any(d => d.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<Guid>($"Custom field '{name.Trim()}' already exists.");
        }

        var definition = new CustomFieldDefinition
        {
            Name = name.Trim(),
            DataType = dataType,
            IsRequired = isRequired,
            SortOrder = existing.Count > 0 ? existing.Max(d => d.SortOrder) + 1 : 0
        };

        await _unitOfWork.CustomFieldDefinitions.AddAsync(definition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Custom field {FieldId} created", definition.Id);
        return Result.Success(definition.Id);
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _unitOfWork.CustomFieldDefinitions.GetByIdAsync(id, cancellationToken);
        if (definition is null || definition.IsArchived)
        {
            return Result.Failure("Custom field not found.");
        }

        definition.IsArchived = true;
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.CustomFieldDefinitions.Update(definition);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Custom field {FieldId} archived", id);
        return Result.Success();
    }

    private static CustomFieldDefinitionDto MapToDto(CustomFieldDefinition definition) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        DataType = definition.DataType,
        IsRequired = definition.IsRequired,
        SortOrder = definition.SortOrder
    };
}
