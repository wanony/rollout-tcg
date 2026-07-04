namespace TCGTrading.Alerts.Application.DTOs;

public record CreateAlertRequest(
    Guid UserId,
    string ProviderCardId,
    string CardName,
    string Direction,
    decimal TargetPriceUsd,
    bool Recurring);
