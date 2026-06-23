using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCGTrading.Marketplace.Infrastructure.Persistence;

public class MarketplaceDbContextFactory : IDesignTimeDbContextFactory<MarketplaceDbContext>
{
    public MarketplaceDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=db_marketplace;Username=tcg;Password=dev")
            .Options;
        return new MarketplaceDbContext(opts);
    }
}
