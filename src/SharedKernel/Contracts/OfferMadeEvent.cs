namespace TCGTrading.SharedKernel.Contracts;

public record OfferMadeEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OfferId,
    Guid ListingId,
    Guid CardId,
    Guid BuyerId,
    Guid SellerId,
    decimal OfferedPriceUsd) : IntegrationEvent(EventId, OccurredAt);
