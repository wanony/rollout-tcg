namespace TCGTrading.Portfolio.Application.DTOs;

public record CollectionItemResponse(
    Guid Id,
    Guid UserId,
    Guid CardId,
    string CardName,
    int Quantity,
    string Condition,
    decimal AcquisitionPriceUsd,
    DateTime CreatedAt);
