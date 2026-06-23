using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCGTrading.Portfolio.Infrastructure.Persistence;

public class PortfolioDbContextFactory : IDesignTimeDbContextFactory<PortfolioDbContext>
{
    public PortfolioDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=db_portfolio;Username=tcg;Password=dev")
            .Options;
        return new PortfolioDbContext(opts);
    }
}
