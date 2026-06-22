namespace TCGTrading.SharedKernel.Contracts;

public record CardPriceUpdatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid CardId,
    string ProviderCardId,
    string TcgProvider,
    decimal MarketPriceUsd,
    decimal LowPriceUsd,
    decimal HighPriceUsd) : IntegrationEvent(EventId, OccurredAt);
