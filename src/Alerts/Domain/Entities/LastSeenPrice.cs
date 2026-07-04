namespace TCGTrading.Alerts.Domain.Entities;

public class LastSeenPrice
{
    public string ProviderCardId { get; private set; } = string.Empty;
    public decimal MarketPriceUsd { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private LastSeenPrice() { } // EF Core

    public LastSeenPrice(string providerCardId, decimal marketPriceUsd, DateTime updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCardId);
        ProviderCardId = providerCardId;
        MarketPriceUsd = marketPriceUsd;
        UpdatedAt = updatedAt;
    }

    public void UpdatePrice(decimal marketPriceUsd, DateTime updatedAt)
    {
        MarketPriceUsd = marketPriceUsd;
        UpdatedAt = updatedAt;
    }
}
