using FluentValidation;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Validation;

/// <summary>
/// Validates printer upsert payloads.
/// </summary>
public sealed class PrinterUpsertDtoValidator : AbstractValidator<PrinterUpsertDto>
{
    public PrinterUpsertDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ConnectionString).MaximumLength(512);
        RuleFor(x => x.PaperWidthMm).InclusiveBetween(10, 300);
        RuleFor(x => x.Dpi).InclusiveBetween(72, 600);
        RuleFor(x => x.Darkness).InclusiveBetween(0, 15);
        RuleFor(x => x.Speed).InclusiveBetween(1, 10);
        RuleFor(x => x.Protocol).IsInEnum();
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .When(x => x.Protocol == PrinterProtocol.Windows)
            .WithMessage("Windows printer requires a queue name.");
    }
}
