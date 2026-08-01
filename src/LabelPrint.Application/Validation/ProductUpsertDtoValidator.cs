using FluentValidation;
using LabelPrint.Application.DTOs;

namespace LabelPrint.Application.Validation;

/// <summary>
/// Validates product create/update payloads.
/// </summary>
public sealed class ProductUpsertDtoValidator : AbstractValidator<ProductUpsertDto>
{
    public ProductUpsertDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Barcode)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.Barcode));

        RuleFor(x => x.PriceAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.PriceCurrency)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.WeightValue)
            .GreaterThanOrEqualTo(0)
            .When(x => x.WeightValue.HasValue);

        RuleFor(x => x.ShelfLifeDays)
            .GreaterThan(0)
            .When(x => x.ShelfLifeDays.HasValue);
    }
}
