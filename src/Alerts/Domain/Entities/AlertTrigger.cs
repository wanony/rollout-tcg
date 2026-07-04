namespace TCGTrading.Alerts.Domain.Entities;

public class AlertTrigger
{
    public Guid Id { get; private set; }
    public Guid AlertId { get; private set; }
    public decimal MarketPriceUsd { get; private set; }
    public DateTime FiredAt { get; private set; }

    private AlertTrigger() { } // EF Core

    public AlertTrigger(Guid alertId, decimal marketPriceUsd, DateTime firedAt)
    {
        Id = Guid.NewGuid();
        AlertId = alertId;
        MarketPriceUsd = marketPriceUsd;
        FiredAt = firedAt;
    }
}
