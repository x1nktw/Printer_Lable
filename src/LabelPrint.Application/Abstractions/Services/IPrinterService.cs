using LabelPrint.Application.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// CRUD operations for configured printers.
/// </summary>
public interface IPrinterService
{
    Task<Result<IReadOnlyList<PrinterListItemDto>>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Result<PrinterEditDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateAsync(PrinterUpsertDto dto, CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(Guid id, PrinterUpsertDto dto, CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> SetDefaultAsync(Guid id, CancellationToken cancellationToken = default);
}

public class PrinterListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public PrinterProtocol Protocol { get; init; }

    public string ConnectionString { get; init; } = string.Empty;

    public double PaperWidthMm { get; init; }

    public bool Rotate90 { get; init; }

    public double PrintOffsetXMm { get; init; }

    public double PrintOffsetYMm { get; init; }

    public int Dpi { get; init; }

    public bool IsDefault { get; init; }

    public bool IsActive { get; init; }
}

public sealed class PrinterEditDto : PrinterListItemDto
{
    public int Darkness { get; init; }

    public int Speed { get; init; }

    public string? Notes { get; init; }
}

public sealed class PrinterUpsertDto
{
    public string Name { get; init; } = string.Empty;

    public PrinterProtocol Protocol { get; init; } = PrinterProtocol.File;

    public string ConnectionString { get; init; } = string.Empty;

    public double PaperWidthMm { get; init; } = 58;

    /// <summary>Force 90° rotation (portrait template on landscape label stock).</summary>
    public bool Rotate90 { get; init; }

    /// <summary>Extra Windows print shift X (mm). Positive → right.</summary>
    public double PrintOffsetXMm { get; init; }

    /// <summary>Extra Windows print shift Y (mm). Positive → down. Top clipped → try negative (e.g. -2).</summary>
    public double PrintOffsetYMm { get; init; }

    public int Dpi { get; init; } = 203;

    public int Darkness { get; init; } = 8;

    public int Speed { get; init; } = 4;

    public bool IsDefault { get; init; }

    public string? Notes { get; init; }
}
