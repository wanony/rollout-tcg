namespace TCGTrading.Portfolio.Application.DTOs;

public record AddCardRequest(
    Guid UserId,
    Guid CardId,
    string CardName,
    int Quantity,
    string Condition,
    decimal AcquisitionPriceUsd);
