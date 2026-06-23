using Microsoft.EntityFrameworkCore;
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Infrastructure.Persistence;

public sealed class CardCatalogDbContext(DbContextOptions<CardCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<Card> Cards => Set<Card>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.Property(c => c.Set).IsRequired().HasMaxLength(200);
            e.Property(c => c.Rarity).HasConversion<int>();
            e.Property(c => c.Type).IsRequired().HasMaxLength(100);
            e.Property(c => c.Text).IsRequired().HasMaxLength(2000);
        });
    }
}
