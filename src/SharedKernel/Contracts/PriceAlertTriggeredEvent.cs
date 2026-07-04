namespace TCGTrading.SharedKernel.Contracts;

public record PriceAlertTriggeredEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AlertId,
    Guid UserId,
    string ProviderCardId,
    string CardName,
    string Direction,
    decimal TargetPriceUsd,
    decimal MarketPriceUsd) : IntegrationEvent(EventId, OccurredAt);
