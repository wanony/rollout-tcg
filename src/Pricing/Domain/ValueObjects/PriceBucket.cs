namespace TCGTrading.Pricing.Domain.ValueObjects;

public sealed class PriceBucket
{
    public DateTimeOffset BucketStart { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Average { get; init; }
}
