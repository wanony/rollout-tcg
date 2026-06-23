using Microsoft.EntityFrameworkCore;
using TCGTrading.Marketplace.Domain.Entities;
using TCGTrading.Marketplace.Domain.Enums;

namespace TCGTrading.Marketplace.Infrastructure.Persistence;

public class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : DbContext(options)
{
    public DbSet<Listing> Listings => Set<Listing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Listing>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CardName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Condition).HasMaxLength(50).IsRequired();
            e.Property(x => x.AskingPriceUsd).HasPrecision(18, 2);
            e.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
        });
    }
}
