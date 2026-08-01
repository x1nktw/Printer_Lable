namespace LabelPrint.Domain.ValueObjects;

/// <summary>
/// Physical label size in millimeters.
/// </summary>
public sealed class LabelSize : IEquatable<LabelSize>
{
    public LabelSize(double widthMm, double heightMm)
    {
        if (widthMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthMm));
        }

        if (heightMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightMm));
        }

        WidthMm = widthMm;
        HeightMm = heightMm;
    }

    public double WidthMm { get; }

    public double HeightMm { get; }

    public bool Equals(LabelSize? other) =>
        other is not null && WidthMm.Equals(other.WidthMm) && HeightMm.Equals(other.HeightMm);

    public override bool Equals(object? obj) => obj is LabelSize other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(WidthMm, HeightMm);

    public override string ToString() => $"{WidthMm}x{HeightMm}";

    public static LabelSize Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Split('x', 'X', '×');
        if (parts.Length != 2 ||
            !double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width) ||
            !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height))
        {
            throw new FormatException($"Invalid label size '{value}'. Expected WxH (e.g. 58x40).");
        }

        return new LabelSize(width, height);
    }
}
