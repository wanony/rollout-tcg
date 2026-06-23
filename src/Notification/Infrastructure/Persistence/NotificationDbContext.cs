using Microsoft.EntityFrameworkCore;
using TCGTrading.Notification.Domain.Entities;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserNotification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
            e.HasIndex(x => x.UserId);
        });
    }
}
