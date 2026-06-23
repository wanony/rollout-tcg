using Microsoft.EntityFrameworkCore;
using TCGTrading.Portfolio.Application.Interfaces;
using TCGTrading.Portfolio.Domain.Entities;

namespace TCGTrading.Portfolio.Infrastructure.Persistence;

public class CollectionRepository(PortfolioDbContext db) : ICollectionRepository
{
    public async Task AddAsync(CollectionItem item, CancellationToken ct = default)
    {
        await db.CollectionItems.AddAsync(item, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CollectionItem>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.CollectionItems
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<CollectionItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.CollectionItems.FindAsync([id], ct);

    public async Task DeleteAsync(CollectionItem item, CancellationToken ct = default)
    {
        db.CollectionItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }
}
