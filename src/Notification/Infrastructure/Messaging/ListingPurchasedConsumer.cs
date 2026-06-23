using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TCGTrading.Notification.Application.DTOs;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Domain.Enums;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Notification.Infrastructure.Messaging;

public class ListingPurchasedConsumer(
    INotificationRepository repo,
    IHubContext<NotificationHub> hub) : IConsumer<ListingPurchasedEvent>
{
    public async Task Consume(ConsumeContext<ListingPurchasedEvent> context)
    {
        var e = context.Message;

        var buyerNotif = UserNotification.Create(
            e.BuyerId,
            title: "Purchase confirmed",
            message: $"You purchased a card for ${e.PurchasePriceUsd:F2}.",
            NotificationType.ListingPurchased,
            referenceId: e.ListingId);

        var sellerNotif = UserNotification.Create(
            e.SellerId,
            title: "Your listing sold",
            message: $"Your listing sold for ${e.PurchasePriceUsd:F2}.",
            NotificationType.ListingSold,
            referenceId: e.ListingId);

        await repo.AddAsync(buyerNotif, context.CancellationToken);
        await repo.AddAsync(sellerNotif, context.CancellationToken);

        await PushAsync(e.BuyerId, buyerNotif);
        await PushAsync(e.SellerId, sellerNotif);
    }

    private async Task PushAsync(Guid userId, UserNotification notif)
    {
        var response = new NotificationResponse(
            notif.Id, notif.UserId, notif.Title, notif.Message,
            notif.Type.ToString(), notif.ReferenceId, notif.IsRead, notif.CreatedAt);

        await hub.Clients.User(userId.ToString()).SendAsync("notification", response);
    }
}
