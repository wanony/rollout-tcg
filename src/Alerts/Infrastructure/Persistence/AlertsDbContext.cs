using MassTransit;
using Microsoft.EntityFrameworkCore;
using TCGTrading.Alerts.Domain.Entities;

namespace TCGTrading.Alerts.Infrastructure.Persistence;

public class AlertsDbContext(DbContextOptions<AlertsDbContext> options) : DbContext(options)
{
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<AlertTrigger> AlertTriggers => Set<AlertTrigger>();
    public DbSet<LastSeenPrice> LastSeenPrices => Set<LastSeenPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PriceAlert>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderCardId).HasMaxLength(100).IsRequired();
            e.Property(x => x.CardName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.ProviderCardId);
        });

        modelBuilder.Entity<AlertTrigger>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AlertId, x.MarketPriceUsd }).IsUnique();
        });

        modelBuilder.Entity<LastSeenPrice>(e =>
        {
            e.HasKey(x => x.ProviderCardId);
            e.Property(x => x.ProviderCardId).HasMaxLength(100);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
