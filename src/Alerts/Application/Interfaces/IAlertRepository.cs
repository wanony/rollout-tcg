using TCGTrading.Alerts.Domain.Entities;

namespace TCGTrading.Alerts.Application.Interfaces;

public interface IAlertRepository
{
    Task AddAsync(PriceAlert alert, CancellationToken ct = default);
    Task<IReadOnlyList<PriceAlert>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<PriceAlert?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PriceAlert>> GetActiveByCardAsync(string providerCardId, CancellationToken ct = default);
    Task<LastSeenPrice?> GetLastSeenAsync(string providerCardId, CancellationToken ct = default);
    Task<bool> TriggerExistsAsync(Guid alertId, Guid sourceEventId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task DeleteAsync(PriceAlert alert, CancellationToken ct = default);
    Task UpsertLastSeenAsync(string providerCardId, decimal price, DateTime at, CancellationToken ct = default);
    Task AddTriggerAsync(AlertTrigger trigger, CancellationToken ct = default);
}
