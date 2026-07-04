using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Domain.Enums;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.Notification.Infrastructure.Messaging;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Notification.Tests.Unit;

public class PriceAlertTriggeredConsumerTests
{
    [Fact]
    public async Task Consume_CreatesNotification_ForUser_AndPushesToHub()
    {
        var repo = Substitute.For<INotificationRepository>();
        var hub = Substitute.For<IHubContext<NotificationHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.User(Arg.Any<string>()).Returns(proxy);

        var consumer = new PriceAlertTriggeredConsumer(repo, hub);

        var userId = Guid.NewGuid();
        var alertId = Guid.NewGuid();

        var @event = new PriceAlertTriggeredEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            AlertId: alertId,
            UserId: userId,
            ProviderCardId: "basep-1",
            CardName: "Charizard",
            Direction: "Above",
            TargetPriceUsd: 100.00m,
            MarketPriceUsd: 120.50m);

        var ctx = Substitute.For<ConsumeContext<PriceAlertTriggeredEvent>>();
        ctx.Message.Returns(@event);
        ctx.CancellationToken.Returns(CancellationToken.None);

        UserNotification? captured = null;
        await repo.AddAsync(
            Arg.Do<UserNotification>(n => captured = n), Arg.Any<CancellationToken>());
        repo.ClearReceivedCalls();

        await consumer.Consume(ctx);

        await repo.Received(1).AddAsync(
            Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.Type.Should().Be(NotificationType.PriceAlert);
        captured.ReferenceId.Should().Be(alertId);
        captured.Message.Should().Be("Charizard is now $120.50 (above your $100.00 target).");

        await proxy.Received(1).SendCoreAsync(
            "notification", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
