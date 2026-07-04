using Microsoft.EntityFrameworkCore;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public class WatchedCardRepository(PricingOutboxDbContext db) : IWatchedCardRepository
{
    public async Task<IReadOnlyList<WatchedCard>> GetAllAsync(CancellationToken ct = default)
        => await db.WatchedCards.AsNoTracking().ToListAsync(ct);

    public async Task IncrementAsync(string providerCardId, Guid cardId, CancellationToken ct = default)
    {
        var watched = await db.WatchedCards.FindAsync([providerCardId], ct);
        if (watched is null)
        {
            watched = new WatchedCard
            {
                ProviderCardId = providerCardId,
                CardId = cardId,
                RefCount = 1,
                UpdatedAt = DateTime.UtcNow,
            };
            db.WatchedCards.Add(watched);
        }
        else
        {
            watched.RefCount++;
            if (cardId != Guid.Empty)
                watched.CardId = cardId;
            watched.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DecrementAsync(string providerCardId, CancellationToken ct = default)
    {
        var watched = await db.WatchedCards.FindAsync([providerCardId], ct);
        if (watched is null)
            return;

        watched.RefCount--;
        if (watched.RefCount <= 0)
        {
            db.WatchedCards.Remove(watched);
        }
        else
        {
            watched.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
