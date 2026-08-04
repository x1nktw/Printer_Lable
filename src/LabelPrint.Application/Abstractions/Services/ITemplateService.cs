using LabelPrint.Application.Common;
using LabelPrint.Domain.Templates;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Label template application service.
/// </summary>
public interface ITemplateService
{
    Task<Result<(IReadOnlyList<TemplateListItemDto> Items, int TotalCount)>> SearchAsync(
        string? search,
        bool includeArchived,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateEditDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateAsync(string name, double widthMm, double heightMm, CancellationToken cancellationToken = default);

    /// <summary>Creates a template from exported ContentJson (optionally renaming).</summary>
    Task<Result<Guid>> ImportFromJsonAsync(string json, string? preferredName = null, CancellationToken cancellationToken = default);

    Task<Result> SaveDocumentAsync(Guid id, string name, TemplateDocument document, CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<Guid>> DuplicateAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class TemplateListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public double WidthMm { get; init; }

    public double HeightMm { get; init; }

    public int SchemaVersion { get; init; }

    public bool IsSystemPreset { get; init; }

    /// <summary>True when selected as print template on Orders or Marking pages.</summary>
    public bool IsInUse { get; init; }

    public bool IsArchived { get; init; }
}

public sealed class TemplateEditDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public double WidthMm { get; init; }

    public double HeightMm { get; init; }

    public int Dpi { get; init; }

    public int SchemaVersion { get; init; }

    public bool IsSystemPreset { get; init; }

    public TemplateDocument Document { get; init; } = new();
}
