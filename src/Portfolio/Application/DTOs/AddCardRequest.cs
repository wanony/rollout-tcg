namespace TCGTrading.Portfolio.Application.DTOs;

public record AddCardRequest(
    Guid UserId,
    string CardId,
    string CardName,
    int Quantity,
    string Condition,
    decimal AcquisitionPriceUsd);
