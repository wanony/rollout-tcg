using MassTransit;
using Microsoft.EntityFrameworkCore;
using TCGTrading.Pricing.Domain.Entities;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

// Separate EF Core relational context for the WatchedCard projection + MassTransit outbox/inbox.
// The time-series price data continues to live in TimescaleDB via Dapper (TimescaleDbPriceRepository) —
// this context deliberately does NOT touch that path.
public class PricingOutboxDbContext(DbContextOptions<PricingOutboxDbContext> options) : DbContext(options)
{
    public DbSet<WatchedCard> WatchedCards => Set<WatchedCard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WatchedCard>(e =>
        {
            e.HasKey(x => x.ProviderCardId);
            e.Property(x => x.ProviderCardId).HasMaxLength(100);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
