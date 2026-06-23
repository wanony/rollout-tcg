using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.Notification.Infrastructure.Messaging;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Notification.Tests.Unit;

public class ListingPurchasedConsumerTests
{
    [Fact]
    public async Task Consume_CreatesNotifications_ForBothBuyerAndSeller()
    {
        var repo = Substitute.For<INotificationRepository>();
        var hub = Substitute.For<IHubContext<NotificationHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.User(Arg.Any<string>()).Returns(proxy);

        var consumer = new ListingPurchasedConsumer(repo, hub);

        var @event = new ListingPurchasedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid(),
            BuyerId: Guid.NewGuid(),
            SellerId: Guid.NewGuid(),
            PurchasePriceUsd: 15.00m);

        var ctx = Substitute.For<ConsumeContext<ListingPurchasedEvent>>();
        ctx.Message.Returns(@event);
        ctx.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(ctx);

        await repo.Received(2).AddAsync(
            Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());

        await proxy.Received(2).SendCoreAsync(
            "notification", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
