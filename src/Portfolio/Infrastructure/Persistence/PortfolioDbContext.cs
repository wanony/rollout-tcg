using Microsoft.EntityFrameworkCore;
using TCGTrading.Portfolio.Domain.Entities;

namespace TCGTrading.Portfolio.Infrastructure.Persistence;

public class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : DbContext(options)
{
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CollectionItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CardName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Condition).HasMaxLength(50).IsRequired();
            e.Property(x => x.AcquisitionPriceUsd).HasPrecision(18, 2);
        });
    }
}
