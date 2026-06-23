namespace TCGTrading.Marketplace.Application.DTOs;

public record CreateListingRequest(
    Guid SellerId,
    Guid CardId,
    string CardName,
    string Condition,
    decimal AskingPriceUsd);
