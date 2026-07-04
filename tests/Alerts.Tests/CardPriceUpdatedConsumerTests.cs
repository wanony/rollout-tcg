using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCGTrading.Alerts.Application.Interfaces;
using TCGTrading.Alerts.Domain.Entities;
using TCGTrading.Alerts.Domain.Enums;
using TCGTrading.Alerts.Infrastructure.Messaging;
using TCGTrading.Alerts.Infrastructure.Persistence;
using TCGTrading.SharedKernel.Contracts;
using Xunit;

namespace TCGTrading.Alerts.Tests;

public class CardPriceUpdatedConsumerTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private IServiceScope _scope = null!;
    private AlertsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        var dbName = $"alerts-{Guid.NewGuid()}";

        services.AddDbContext<AlertsDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName));
        services.AddScoped<IAlertRepository, AlertRepository>();

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<CardPriceUpdatedConsumer>();
        });

        _provider = services.BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();

        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AlertsDbContext>();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        _scope.Dispose();
        await _provider.DisposeAsync();
    }

    private async Task<PriceAlert> SeedActiveAlert(string providerCardId, AlertDirection direction, decimal target, bool recurring = false)
    {
        var alert = PriceAlert.Create(Guid.NewGuid(), providerCardId, "Test Card", direction, target, recurring);
        _db.PriceAlerts.Add(alert);
        await _db.SaveChangesAsync();
        return alert;
    }

    private async Task SeedLastSeen(string providerCardId, decimal price)
    {
        _db.LastSeenPrices.Add(new LastSeenPrice(providerCardId, price, DateTime.UtcNow));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Publishes_triggered_event_when_alert_crosses()
    {
        var alert = await SeedActiveAlert("basep-1", AlertDirection.Below, 10m);
        await SeedLastSeen("basep-1", 12m);

        await _harness.Bus.Publish(new CardPriceUpdatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "basep-1", "tcgdex", 9m, 8m, 9.5m));

        Assert.True(await _harness.Published.Any<PriceAlertTriggeredEvent>());

        await _db.Entry(alert).ReloadAsync();
        Assert.False(alert.IsActive);
    }

    [Fact]
    public async Task Does_not_publish_when_price_does_not_cross()
    {
        await SeedActiveAlert("basep-2", AlertDirection.Below, 10m);
        await SeedLastSeen("basep-2", 12m);

        await _harness.Bus.Publish(new CardPriceUpdatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "basep-2", "tcgdex", 11m, 10m, 11.5m));

        Assert.True(await _harness.Consumed.Any<CardPriceUpdatedEvent>());
        Assert.False(await _harness.Published.Any<PriceAlertTriggeredEvent>());
    }

    [Fact]
    public async Task Is_idempotent_on_redelivery()
    {
        await SeedActiveAlert("basep-3", AlertDirection.Below, 10m, recurring: true);
        await SeedLastSeen("basep-3", 12m);

        var evt = new CardPriceUpdatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "basep-3", "tcgdex", 9m, 8m, 9.5m);

        await _harness.Bus.Publish(evt);
        await _harness.Bus.Publish(evt);

        Assert.Equal(2, (await _harness.Consumed.SelectAsync<CardPriceUpdatedEvent>().ToListAsync()).Count);
        Assert.Single(await _harness.Published.SelectAsync<PriceAlertTriggeredEvent>().ToListAsync());

        var triggerCount = await _db.AlertTriggers.CountAsync();
        Assert.Equal(1, triggerCount);
    }

    [Fact]
    public async Task Recurring_alert_fires_again_on_distinct_event_at_same_price()
    {
        await SeedActiveAlert("basep-4", AlertDirection.Below, 10m, recurring: true);
        await SeedLastSeen("basep-4", 12m);

        var firstEvent = new CardPriceUpdatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "basep-4", "tcgdex", 9m, 8m, 9.5m);
        await _harness.Bus.Publish(firstEvent);
        Assert.True(await _harness.Consumed.Any<CardPriceUpdatedEvent>(x => x.Context.Message.EventId == firstEvent.EventId));

        // simulate price moving back above target (re-arming the hysteresis band) before
        // re-crossing below via a distinct event
        var lastSeen = await _db.LastSeenPrices.SingleAsync(l => l.ProviderCardId == "basep-4");
        await _db.Entry(lastSeen).ReloadAsync(); // this context's tracked copy is stale vs the
                                                  // consumer's own DbContext instance, which already
                                                  // wrote 9m for the first event
        lastSeen.UpdatePrice(12m, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        var secondEvent = new CardPriceUpdatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "basep-4", "tcgdex", 9m, 8m, 9.5m);
        await _harness.Bus.Publish(secondEvent);
        Assert.True(await _harness.Consumed.Any<CardPriceUpdatedEvent>(x => x.Context.Message.EventId == secondEvent.EventId));


        Assert.Equal(2, (await _harness.Published.SelectAsync<PriceAlertTriggeredEvent>().ToListAsync()).Count);

        var seededAlert = await _db.PriceAlerts.SingleAsync(a => a.ProviderCardId == "basep-4");
        var triggerCount = await _db.AlertTriggers.CountAsync(t => t.AlertId == seededAlert.Id);
        Assert.Equal(2, triggerCount);
    }
}
