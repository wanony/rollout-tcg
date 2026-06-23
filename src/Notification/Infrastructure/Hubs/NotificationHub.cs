using Microsoft.AspNetCore.SignalR;

namespace TCGTrading.Notification.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    // Clients call hub.on("notification", handler) to receive pushed notifications.
    // No server-side methods needed in v1 — hub is push-only.
}
