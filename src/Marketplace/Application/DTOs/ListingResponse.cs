namespace TCGTrading.Marketplace.Application.DTOs;

public record ListingResponse(
    Guid Id,
    Guid SellerId,
    Guid CardId,
    string CardName,
    string Condition,
    decimal AskingPriceUsd,
    string Status,
    Guid? BuyerId,
    DateTime? PurchasedAt,
    DateTime CreatedAt);
