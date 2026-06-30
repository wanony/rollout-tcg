using TCGTrading.Marketplace.Domain.Entities;

namespace TCGTrading.Marketplace.Application.Interfaces;

public interface IOfferRepository
{
    Task AddAsync(Offer offer, CancellationToken ct = default);
    Task<Offer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Offer>> GetByListingIdAsync(Guid listingId, CancellationToken ct = default);
    Task UpdateAsync(Offer offer, CancellationToken ct = default);
}
