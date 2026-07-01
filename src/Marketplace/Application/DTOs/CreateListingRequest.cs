namespace TCGTrading.Marketplace.Application.DTOs;

public record CreateListingRequest(
    Guid SellerId,
    string CardId,
    string CardName,
    string Condition,
    decimal AskingPriceUsd);
