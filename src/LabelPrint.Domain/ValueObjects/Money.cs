namespace LabelPrint.Domain.ValueObjects;

/// <summary>
/// Monetary amount with ISO currency code.
/// </summary>
public sealed class Money : IEquatable<Money>
{
    public Money(decimal amount, string currency = "RUB")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }

    /// <summary>Numeric amount.</summary>
    public decimal Amount { get; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; }

    public bool Equals(Money? other) =>
        other is not null && Amount == other.Amount && Currency == other.Currency;

    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Amount} {Currency}");
}
