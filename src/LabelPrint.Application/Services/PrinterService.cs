using FluentValidation;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Application service for printer CRUD.
/// </summary>
public sealed class PrinterService : IPrinterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<PrinterUpsertDto> _validator;
    private readonly ILogger<PrinterService> _logger;

    public PrinterService(
        IUnitOfWork unitOfWork,
        IValidator<PrinterUpsertDto> validator,
        ILogger<PrinterService> logger)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PrinterListItemDto>>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.Printers.GetAllAsync(includeInactive, cancellationToken);
        return Result.Success<IReadOnlyList<PrinterListItemDto>>(items.Select(MapListItem).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<PrinterEditDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var printer = await _unitOfWork.Printers.GetByIdAsync(id, cancellationToken);
        return printer is null
            ? Result.Failure<PrinterEditDto>("Printer not found.")
            : Result.Success(MapEdit(printer));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(PrinterUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var connectionError = ValidateConnectionString(dto);
        if (connectionError is not null)
        {
            return Result.Failure<Guid>(connectionError);
        }

        if (dto.IsDefault)
        {
            await _unitOfWork.Printers.ClearDefaultFlagAsync(cancellationToken);
        }

        var printer = new Printer();
        ApplyDto(printer, dto);
        await _unitOfWork.Printers.AddAsync(printer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Printer {PrinterId} created ({Name}, {Protocol})", printer.Id, printer.Name, printer.Protocol);
        return Result.Success(printer.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, PrinterUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var printer = await _unitOfWork.Printers.GetByIdAsync(id, cancellationToken);
        if (printer is null)
        {
            return Result.Failure("Printer not found.");
        }

        var connectionError = ValidateConnectionString(dto);
        if (connectionError is not null)
        {
            return Result.Failure(connectionError);
        }

        if (dto.IsDefault && !printer.IsDefault)
        {
            await _unitOfWork.Printers.ClearDefaultFlagAsync(cancellationToken);
        }

        ApplyDto(printer, dto);
        _unitOfWork.Printers.Update(printer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var printer = await _unitOfWork.Printers.GetByIdAsync(id, cancellationToken);
        if (printer is null)
        {
            return Result.Failure("Printer not found.");
        }

        await _unitOfWork.Printers.DeactivateAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var printer = await _unitOfWork.Printers.GetByIdAsync(id, cancellationToken);
        if (printer is null)
        {
            return Result.Failure("Printer not found.");
        }

        if (!printer.IsActive)
        {
            return Result.Failure("Inactive printer cannot be set as default.");
        }

        await _unitOfWork.Printers.ClearDefaultFlagAsync(cancellationToken);
        printer.IsDefault = true;
        _unitOfWork.Printers.Update(printer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static void ApplyDto(Printer printer, PrinterUpsertDto dto)
    {
        printer.Name = dto.Name.Trim();
        printer.Protocol = dto.Protocol;
        printer.ConnectionString = dto.ConnectionString.Trim();
        printer.PaperWidthMm = dto.PaperWidthMm;
        printer.Rotate90 = dto.Rotate90;
        printer.PrintOffsetXMm = dto.PrintOffsetXMm;
        printer.PrintOffsetYMm = dto.PrintOffsetYMm;
        printer.Dpi = dto.Dpi;
        printer.Darkness = dto.Darkness;
        printer.Speed = dto.Speed;
        printer.IsDefault = dto.IsDefault;
        printer.IsActive = true;
        printer.Notes = dto.Notes;
        printer.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? ValidateConnectionString(PrinterUpsertDto dto)
    {
        if (dto.Protocol == PrinterProtocol.Windows && string.IsNullOrWhiteSpace(dto.ConnectionString))
        {
            return "Windows printer requires a queue name in ConnectionString.";
        }

        return null;
    }

    private static PrinterListItemDto MapListItem(Printer printer) => new()
    {
        Id = printer.Id,
        Name = printer.Name,
        Protocol = printer.Protocol,
        ConnectionString = printer.ConnectionString,
        PaperWidthMm = printer.PaperWidthMm,
        Rotate90 = printer.Rotate90,
        PrintOffsetXMm = printer.PrintOffsetXMm,
        PrintOffsetYMm = printer.PrintOffsetYMm,
        Dpi = printer.Dpi,
        IsDefault = printer.IsDefault,
        IsActive = printer.IsActive
    };

    private static PrinterEditDto MapEdit(Printer printer) => new()
    {
        Id = printer.Id,
        Name = printer.Name,
        Protocol = printer.Protocol,
        ConnectionString = printer.ConnectionString,
        PaperWidthMm = printer.PaperWidthMm,
        Rotate90 = printer.Rotate90,
        PrintOffsetXMm = printer.PrintOffsetXMm,
        PrintOffsetYMm = printer.PrintOffsetYMm,
        Dpi = printer.Dpi,
        Darkness = printer.Darkness,
        Speed = printer.Speed,
        IsDefault = printer.IsDefault,
        IsActive = printer.IsActive,
        Notes = printer.Notes
    };
}
