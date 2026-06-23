using TCGTrading.Notification.Domain.Enums;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.Notification.Domain.Entities;

public class UserNotification : Entity
{
    public Guid UserId { get; private init; }
    public string Title { get; private init; } = string.Empty;
    public string Message { get; private init; } = string.Empty;
    public NotificationType Type { get; private init; }
    public Guid? ReferenceId { get; private init; }
    public bool IsRead { get; private set; }

    private UserNotification() { } // EF Core

    public static UserNotification Create(
        Guid userId, string title, string message,
        NotificationType type, Guid? referenceId = null)
        => new()
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ReferenceId = referenceId,
            IsRead = false
        };

    public void MarkRead() => IsRead = true;
}
