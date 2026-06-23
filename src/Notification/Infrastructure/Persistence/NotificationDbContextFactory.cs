using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=db_notification;Username=tcg;Password=dev")
            .Options;
        return new NotificationDbContext(opts);
    }
}
