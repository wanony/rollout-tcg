using TCGTrading.Marketplace.Domain.Entities;

namespace TCGTrading.Marketplace.Application.Interfaces;

public interface IListingRepository
{
    Task AddAsync(Listing listing, CancellationToken ct = default);
    Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Listing>> GetActiveAsync(CancellationToken ct = default);
    Task UpdateAsync(Listing listing, CancellationToken ct = default);
}
