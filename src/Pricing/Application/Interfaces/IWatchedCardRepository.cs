using TCGTrading.Pricing.Domain.Entities;

namespace TCGTrading.Pricing.Application.Interfaces;

public interface IWatchedCardRepository
{
    Task<IReadOnlyList<WatchedCard>> GetAllAsync(CancellationToken ct = default);

    // PriceAlertCreatedEvent doesn't carry CardId, so cardId may be Guid.Empty when unknown.
    Task IncrementAsync(string providerCardId, Guid cardId, CancellationToken ct = default);

    Task DecrementAsync(string providerCardId, CancellationToken ct = default);
}
