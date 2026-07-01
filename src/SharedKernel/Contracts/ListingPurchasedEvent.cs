namespace TCGTrading.SharedKernel.Contracts;

public record ListingPurchasedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ListingId,
    string CardId,
    Guid BuyerId,
    Guid SellerId,
    decimal PurchasePriceUsd) : IntegrationEvent(EventId, OccurredAt);
