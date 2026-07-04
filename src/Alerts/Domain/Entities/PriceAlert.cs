using TCGTrading.Alerts.Domain.Enums;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.Alerts.Domain.Entities;

public class PriceAlert : Entity
{
    public Guid UserId { get; private init; }
    public string ProviderCardId { get; private init; } = string.Empty;
    public string CardName { get; private init; } = string.Empty;
    public AlertDirection Direction { get; private init; }
    public decimal TargetPriceUsd { get; private init; }
    public bool Recurring { get; private init; }
    public bool IsActive { get; private set; }
    public DateTime? LastFiredAt { get; private set; }

    private PriceAlert() { } // EF Core

    public static PriceAlert Create(Guid userId, string providerCardId, string cardName,
        AlertDirection direction, decimal targetPriceUsd, bool recurring)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetPriceUsd);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCardId);
        return new PriceAlert
        {
            UserId = userId, ProviderCardId = providerCardId, CardName = cardName,
            Direction = direction, TargetPriceUsd = targetPriceUsd,
            Recurring = recurring, IsActive = true,
        };
    }

    public void Fire(DateTime at)
    {
        LastFiredAt = at;
        if (!Recurring) IsActive = false;
    }
}
