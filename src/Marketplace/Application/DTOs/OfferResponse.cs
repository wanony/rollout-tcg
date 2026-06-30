namespace TCGTrading.Marketplace.Application.DTOs;

public record OfferResponse(
    Guid Id,
    Guid ListingId,
    Guid BuyerId,
    decimal OfferedPriceUsd,
    string Status,
    DateTime CreatedAt,
    DateTime? RespondedAt);
