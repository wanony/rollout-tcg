using Microsoft.EntityFrameworkCore;
using TCGTrading.Marketplace.Application.Interfaces;
using TCGTrading.Marketplace.Domain.Entities;

namespace TCGTrading.Marketplace.Infrastructure.Persistence;

public class OfferRepository(MarketplaceDbContext db) : IOfferRepository
{
    public async Task AddAsync(Offer offer, CancellationToken ct = default)
    {
        await db.Offers.AddAsync(offer, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Offer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Offers.FindAsync([id], ct);

    public async Task<IReadOnlyList<Offer>> GetByListingIdAsync(Guid listingId, CancellationToken ct = default)
        => await db.Offers
            .Where(o => o.ListingId == listingId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public async Task UpdateAsync(Offer offer, CancellationToken ct = default)
    {
        db.Offers.Update(offer);
        await db.SaveChangesAsync(ct);
    }
}
