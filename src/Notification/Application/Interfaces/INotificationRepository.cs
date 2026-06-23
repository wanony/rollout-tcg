using TCGTrading.Notification.Domain.Entities;

namespace TCGTrading.Notification.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(UserNotification notification, CancellationToken ct = default);
    Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserNotification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(UserNotification notification, CancellationToken ct = default);
}
