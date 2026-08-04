using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.Templates;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Templates;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Template library application service.
/// </summary>
public sealed class TemplateService : ITemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(IUnitOfWork unitOfWork, ILogger<TemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<(IReadOnlyList<TemplateListItemDto> Items, int TotalCount)>> SearchAsync(
        string? search,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take is <= 0 or > 500)
        {
            return Result.Failure<(IReadOnlyList<TemplateListItemDto>, int)>("Take must be between 1 and 500.");
        }

        var (items, total) = await _unitOfWork.Templates.SearchAsync(search, includeArchived, skip, take, cancellationToken);
        var settings = await _unitOfWork.Settings.GetAsync(cancellationToken);
        var usedIds = new HashSet<Guid>();
        if (settings.OrdersPrintTemplateId is Guid ordersTid)
        {
            usedIds.Add(ordersTid);
        }

        if (settings.MarkingPrintTemplateId is Guid markingTid)
        {
            usedIds.Add(markingTid);
        }

        var dtos = items.Select(t => new TemplateListItemDto
        {
            Id = t.Id,
            Name = t.Name,
            WidthMm = t.WidthMm,
            HeightMm = t.HeightMm,
            SchemaVersion = t.SchemaVersion,
            IsSystemPreset = t.IsSystemPreset,
            IsInUse = usedIds.Contains(t.Id),
            IsArchived = t.IsArchived
        }).ToList();

        return Result.Success<(IReadOnlyList<TemplateListItemDto>, int)>((dtos, total));
    }

    /// <inheritdoc />
    public async Task<Result<TemplateEditDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.Templates.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Result.Failure<TemplateEditDto>("Шаблон не найден.");
        }

        var document = TemplateDocumentSerializer.Deserialize(template.ContentJson);
        document.TemplateId ??= template.Id.ToString();
        document.Name ??= template.Name;
        document.Canvas.WidthMm = template.WidthMm;
        document.Canvas.HeightMm = template.HeightMm;
        document.Canvas.Dpi = template.Dpi;

        return Result.Success(new TemplateEditDto
        {
            Id = template.Id,
            Name = template.Name,
            WidthMm = template.WidthMm,
            HeightMm = template.HeightMm,
            Dpi = template.Dpi,
            SchemaVersion = template.SchemaVersion,
            IsSystemPreset = template.IsSystemPreset,
            Document = document
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(string name, double widthMm, double heightMm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Guid>("Название шаблона обязательно.");
        }

        if (widthMm <= 0 || heightMm <= 0)
        {
            return Result.Failure<Guid>("Размер этикетки должен быть больше нуля.");
        }

        var document = new TemplateDocument
        {
            SchemaVersion = 1,
            Name = name.Trim(),
            Canvas = new TemplateCanvas { WidthMm = widthMm, HeightMm = heightMm, Dpi = 203 }
        };

        var template = new LabelTemplate
        {
            Name = name.Trim(),
            WidthMm = widthMm,
            HeightMm = heightMm,
            Dpi = 203,
            SchemaVersion = 1,
            ContentJson = TemplateDocumentSerializer.Serialize(document)
        };

        document.TemplateId = template.Id.ToString();
        template.ContentJson = TemplateDocumentSerializer.Serialize(document);

        await _unitOfWork.Templates.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Template {TemplateId} created", template.Id);
        return Result.Success(template.Id);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> ImportFromJsonAsync(
        string json,
        string? preferredName = null,
        CancellationToken cancellationToken = default)
    {
        if (!TemplateDocumentSerializer.TryDeserialize(json, out var document))
        {
            return Result.Failure<Guid>("Некорректный JSON шаблона.");
        }

        var name = !string.IsNullOrWhiteSpace(preferredName)
            ? preferredName.Trim()
            : (string.IsNullOrWhiteSpace(document.Name) ? "Импортированный шаблон" : document.Name.Trim());

        var create = await CreateAsync(name, document.Canvas.WidthMm, document.Canvas.HeightMm, cancellationToken);
        if (create.IsFailure)
        {
            return create;
        }

        document.Name = name;
        var save = await SaveDocumentAsync(create.Value, name, document, cancellationToken);
        return save.IsFailure ? Result.Failure<Guid>(save.Error!) : Result.Success(create.Value);
    }

    /// <inheritdoc />
    public async Task<Result> SaveDocumentAsync(Guid id, string name, TemplateDocument document, CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.Templates.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Result.Failure("Шаблон не найден.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Название шаблона обязательно.");
        }

        document.SchemaVersion = 1;
        document.TemplateId = template.Id.ToString();
        document.Name = name.Trim();
        template.Name = name.Trim();
        template.WidthMm = document.Canvas.WidthMm;
        template.HeightMm = document.Canvas.HeightMm;
        template.Dpi = document.Canvas.Dpi <= 0 ? 203 : document.Canvas.Dpi;
        template.SchemaVersion = document.SchemaVersion;
        template.ContentJson = TemplateDocumentSerializer.Serialize(document);
        template.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Templates.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Template {TemplateId} saved", id);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.Templates.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Result.Failure("Шаблон не найден.");
        }

        var usedIds = new HashSet<Guid>();
        var settings = await _unitOfWork.Settings.GetAsync(cancellationToken);
        if (settings.OrdersPrintTemplateId is Guid ordersTid)
        {
            usedIds.Add(ordersTid);
        }

        if (settings.MarkingPrintTemplateId is Guid markingTid)
        {
            usedIds.Add(markingTid);
        }

        if (usedIds.Contains(id))
        {
            return Result.Failure("Шаблон выбран в Заказах или Маркировке и его нельзя удалить.");
        }

        await _unitOfWork.Templates.SoftArchiveAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> DuplicateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await GetAsync(id, cancellationToken);
        if (source.IsFailure)
        {
            return Result.Failure<Guid>(source.Error!);
        }

        var dto = source.Value;
        var create = await CreateAsync($"{dto.Name} (копия)", dto.WidthMm, dto.HeightMm, cancellationToken);
        if (create.IsFailure)
        {
            return create;
        }

        dto.Document.Name = $"{dto.Name} (копия)";
        var save = await SaveDocumentAsync(create.Value, dto.Document.Name!, dto.Document, cancellationToken);
        return save.IsFailure ? Result.Failure<Guid>(save.Error!) : Result.Success(create.Value);
    }
}
