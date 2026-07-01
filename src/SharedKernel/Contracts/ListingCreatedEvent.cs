namespace TCGTrading.SharedKernel.Contracts;

public record ListingCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ListingId,
    string CardId,
    string CardName,
    Guid SellerId,
    decimal AskingPriceUsd,
    string Condition) : IntegrationEvent(EventId, OccurredAt);
