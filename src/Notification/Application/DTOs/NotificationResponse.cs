namespace TCGTrading.Notification.Application.DTOs;

public record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    string Type,
    Guid? ReferenceId,
    bool IsRead,
    DateTime CreatedAt);
