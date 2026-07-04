namespace TCGTrading.Pricing.Domain.Entities;

public class WatchedCard
{
    public string ProviderCardId { get; set; } = string.Empty;
    public Guid CardId { get; set; }
    public int RefCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
