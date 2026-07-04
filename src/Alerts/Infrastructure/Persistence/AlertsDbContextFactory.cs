using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCGTrading.Alerts.Infrastructure.Persistence;

public class AlertsDbContextFactory : IDesignTimeDbContextFactory<AlertsDbContext>
{
    public AlertsDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AlertsDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=db_alerts;Username=tcg;Password=dev")
            .Options;
        return new AlertsDbContext(opts);
    }
}
