namespace TCGTrading.SharedKernel.Contracts;

public record CardCatalogSyncedEvent(
    Guid EventId,
    DateTime OccurredAt,
    string TcgProvider,
    IReadOnlyList<string> ProviderCardIds) : IntegrationEvent(EventId, OccurredAt);
