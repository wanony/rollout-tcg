namespace TCGTrading.Marketplace.Application.DTOs;

public record MakeOfferRequest(Guid BuyerId, decimal OfferedPriceUsd);
