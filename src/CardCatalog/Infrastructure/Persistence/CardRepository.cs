using Microsoft.EntityFrameworkCore;
using TCGTrading.CardCatalog.Application.Interfaces;
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Infrastructure.Persistence;

public sealed class CardRepository(CardCatalogDbContext db) : ICardRepository
{
    public async Task<Card> AddAsync(Card card, CancellationToken ct = default)
    {
        db.Cards.Add(card);
        await db.SaveChangesAsync(ct);
        return card;
    }

    public async Task<IReadOnlyList<Card>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Cards.AsNoTracking().ToListAsync(ct);
    }
}
