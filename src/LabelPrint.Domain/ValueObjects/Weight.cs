using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.ValueObjects;

/// <summary>
/// Product weight with explicit unit.
/// </summary>
public sealed class Weight : IEquatable<Weight>
{
    public Weight(decimal value, WeightUnit unit = WeightUnit.Kilogram)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Weight cannot be negative.");
        }

        Value = value;
        Unit = unit;
    }

    /// <summary>Numeric weight value.</summary>
    public decimal Value { get; }

    /// <summary>Measurement unit.</summary>
    public WeightUnit Unit { get; }

    /// <summary>Converts to kilograms.</summary>
    public decimal ToKilograms() => Unit == WeightUnit.Gram ? Value / 1000m : Value;

    public bool Equals(Weight? other) =>
        other is not null && Value == other.Value && Unit == other.Unit;

    public override bool Equals(object? obj) => obj is Weight other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Unit);

    public override string ToString() => Unit == WeightUnit.Gram ? $"{Value} g" : $"{Value} kg";
}
