using Microsoft.EntityFrameworkCore;
using TCGTrading.Marketplace.Application.Interfaces;
using TCGTrading.Marketplace.Domain.Entities;
using TCGTrading.Marketplace.Domain.Enums;

namespace TCGTrading.Marketplace.Infrastructure.Persistence;

public class ListingRepository(MarketplaceDbContext db) : IListingRepository
{
    public async Task AddAsync(Listing listing, CancellationToken ct = default)
    {
        await db.Listings.AddAsync(listing, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Listings.FindAsync([id], ct);

    public async Task<IReadOnlyList<Listing>> GetActiveAsync(CancellationToken ct = default)
        => await db.Listings
            .Where(l => l.Status == ListingStatus.Active)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task UpdateAsync(Listing listing, CancellationToken ct = default)
    {
        db.Listings.Update(listing);
        await db.SaveChangesAsync(ct);
    }
}
