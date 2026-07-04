using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TCGTrading.Notification.Application.DTOs;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Domain.Enums;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Notification.Infrastructure.Messaging;

public class PriceAlertTriggeredConsumer(
    INotificationRepository repo,
    IHubContext<NotificationHub> hub) : IConsumer<PriceAlertTriggeredEvent>
{
    public async Task Consume(ConsumeContext<PriceAlertTriggeredEvent> context)
    {
        var e = context.Message;

        var notif = UserNotification.Create(
            e.UserId,
            title: "Price alert",
            message: $"{e.CardName} is now ${e.MarketPriceUsd:F2} ({e.Direction.ToLower()} your ${e.TargetPriceUsd:F2} target).",
            NotificationType.PriceAlert,
            referenceId: e.AlertId);

        await repo.AddAsync(notif, context.CancellationToken);

        await PushAsync(e.UserId, notif);
    }

    private async Task PushAsync(Guid userId, UserNotification notif)
    {
        var response = new NotificationResponse(
            notif.Id, notif.UserId, notif.Title, notif.Message,
            notif.Type.ToString(), notif.ReferenceId, notif.IsRead, notif.CreatedAt);

        await hub.Clients.User(userId.ToString()).SendAsync("notification", response);
    }
}
