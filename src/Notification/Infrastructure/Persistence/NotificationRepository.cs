using Microsoft.EntityFrameworkCore;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task AddAsync(UserNotification notification, CancellationToken ct = default)
    {
        await db.UserNotifications.AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.UserNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task<UserNotification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.UserNotifications.FindAsync([id], ct);

    public async Task UpdateAsync(UserNotification notification, CancellationToken ct = default)
    {
        db.UserNotifications.Update(notification);
        await db.SaveChangesAsync(ct);
    }
}
