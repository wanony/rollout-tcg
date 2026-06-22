namespace TCGTrading.SharedKernel.Contracts;

public record OfferAcceptedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OfferId,
    Guid ListingId,
    Guid CardId,
    Guid BuyerId,
    Guid SellerId,
    decimal FinalPriceUsd) : IntegrationEvent(EventId, OccurredAt);
