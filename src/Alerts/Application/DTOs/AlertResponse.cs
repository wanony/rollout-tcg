namespace TCGTrading.Alerts.Application.DTOs;

public record AlertResponse(
    Guid Id,
    Guid UserId,
    string ProviderCardId,
    string CardName,
    string Direction,
    decimal TargetPriceUsd,
    bool Recurring,
    bool IsActive,
    DateTime? LastFiredAt,
    DateTime CreatedAt);
