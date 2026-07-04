using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public class PricingOutboxDbContextFactory : IDesignTimeDbContextFactory<PricingOutboxDbContext>
{
    public PricingOutboxDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<PricingOutboxDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=db_pricing_outbox;Username=tcg;Password=dev")
            .Options;
        return new PricingOutboxDbContext(opts);
    }
}
