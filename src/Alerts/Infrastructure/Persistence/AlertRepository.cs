using Microsoft.EntityFrameworkCore;
using TCGTrading.Alerts.Application.Interfaces;
using TCGTrading.Alerts.Domain.Entities;

namespace TCGTrading.Alerts.Infrastructure.Persistence;

public class AlertRepository(AlertsDbContext db) : IAlertRepository
{
    public async Task AddAsync(PriceAlert alert, CancellationToken ct = default)
    {
        await db.PriceAlerts.AddAsync(alert, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PriceAlert>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.PriceAlerts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<PriceAlert?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.PriceAlerts.FindAsync([id], ct);

    public async Task<IReadOnlyList<PriceAlert>> GetActiveByCardAsync(string providerCardId, CancellationToken ct = default)
        => await db.PriceAlerts
            .Where(a => a.ProviderCardId == providerCardId && a.IsActive)
            .ToListAsync(ct);

    public async Task<LastSeenPrice?> GetLastSeenAsync(string providerCardId, CancellationToken ct = default)
        => await db.LastSeenPrices.FindAsync([providerCardId], ct);

    public async Task<bool> TriggerExistsAsync(Guid alertId, decimal marketPriceUsd, CancellationToken ct = default)
        => await db.AlertTriggers.AnyAsync(t => t.AlertId == alertId && t.MarketPriceUsd == marketPriceUsd, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task DeleteAsync(PriceAlert alert, CancellationToken ct = default)
    {
        db.PriceAlerts.Remove(alert);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpsertLastSeenAsync(string providerCardId, decimal price, DateTime at, CancellationToken ct = default)
    {
        var existing = await db.LastSeenPrices.FindAsync([providerCardId], ct);
        if (existing is null)
        {
            await db.LastSeenPrices.AddAsync(new LastSeenPrice(providerCardId, price, at), ct);
        }
        else
        {
            existing.UpdatePrice(price, at);
        }
    }

    public async Task AddTriggerAsync(AlertTrigger trigger, CancellationToken ct = default)
        => await db.AlertTriggers.AddAsync(trigger, ct);
}
