namespace TCGTrading.Pricing.Domain.Entities;

public sealed class PriceSnapshot
{
    public Guid CardId { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }

    private PriceSnapshot() { }

    public static PriceSnapshot Create(
        Guid cardId,
        decimal price,
        string currency,
        string source,
        DateTimeOffset recordedAt)
    {
        return new PriceSnapshot
        {
            CardId = cardId,
            Price = price,
            Currency = currency,
            Source = source,
            RecordedAt = recordedAt,
        };
    }
}
