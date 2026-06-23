using TCGTrading.Portfolio.Domain.Entities;

namespace TCGTrading.Portfolio.Application.Interfaces;

public interface ICollectionRepository
{
    Task AddAsync(CollectionItem item, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionItem>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<CollectionItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(CollectionItem item, CancellationToken ct = default);
}
