namespace TCGTrading.SharedKernel.Contracts;

public record PriceAlertDeletedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AlertId,
    string ProviderCardId) : IntegrationEvent(EventId, OccurredAt);
