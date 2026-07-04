namespace TCGTrading.Alerts.Domain.Entities;

public class AlertTrigger
{
    public Guid Id { get; private set; }
    public Guid AlertId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public decimal MarketPriceUsd { get; private set; }
    public DateTime FiredAt { get; private set; }

    private AlertTrigger() { } // EF Core

    public AlertTrigger(Guid alertId, Guid sourceEventId, decimal marketPriceUsd, DateTime firedAt)
    {
        Id = Guid.NewGuid();
        AlertId = alertId;
        SourceEventId = sourceEventId;
        MarketPriceUsd = marketPriceUsd;
        FiredAt = firedAt;
    }
}
