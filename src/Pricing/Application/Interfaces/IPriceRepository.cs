using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Domain.Enums;
using TCGTrading.Pricing.Domain.ValueObjects;

namespace TCGTrading.Pricing.Application.Interfaces;

public interface IPriceRepository
{
    Task AddAsync(PriceSnapshot snapshot, CancellationToken ct = default);

    Task<IReadOnlyList<PriceBucket>> GetHistoryAsync(
        Guid cardId,
        DateTimeOffset from,
        DateTimeOffset to,
        Granularity granularity,
        CancellationToken ct = default);

    Task<bool> HasAnyAsync(CancellationToken ct = default);
}
