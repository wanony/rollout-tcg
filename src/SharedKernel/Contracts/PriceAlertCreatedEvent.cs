namespace TCGTrading.SharedKernel.Contracts;

public record PriceAlertCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AlertId,
    string ProviderCardId) : IntegrationEvent(EventId, OccurredAt);
