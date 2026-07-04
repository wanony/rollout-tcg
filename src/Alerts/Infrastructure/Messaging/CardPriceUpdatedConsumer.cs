using MassTransit;
using TCGTrading.Alerts.Application.Interfaces;
using TCGTrading.Alerts.Domain;
using TCGTrading.Alerts.Domain.Entities;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Alerts.Infrastructure.Messaging;

public class CardPriceUpdatedConsumer(IAlertRepository repo) : IConsumer<CardPriceUpdatedEvent>
{
    const decimal Hysteresis = 0.02m; // 2% band

    public async Task Consume(ConsumeContext<CardPriceUpdatedEvent> ctx)
    {
        var e = ctx.Message;
        var last = await repo.GetLastSeenAsync(e.ProviderCardId, ctx.CancellationToken);
        var alerts = await repo.GetActiveByCardAsync(e.ProviderCardId, ctx.CancellationToken);

        foreach (var alert in alerts)
        {
            if (!CrossingPolicy.ShouldFire(alert.Direction, alert.TargetPriceUsd,
                    last?.MarketPriceUsd, e.MarketPriceUsd, Hysteresis))
                continue;

            // idempotency: same (alert, source event) already recorded → skip
            if (await repo.TriggerExistsAsync(alert.Id, e.EventId, ctx.CancellationToken))
                continue;

            alert.Fire(e.OccurredAt);
            await repo.AddTriggerAsync(new AlertTrigger(alert.Id, e.EventId, e.MarketPriceUsd, e.OccurredAt), ctx.CancellationToken);
            await ctx.Publish(new PriceAlertTriggeredEvent(
                Guid.NewGuid(), DateTime.UtcNow, alert.Id, alert.UserId,
                alert.ProviderCardId, alert.CardName, alert.Direction.ToString(),
                alert.TargetPriceUsd, e.MarketPriceUsd));
        }

        await repo.UpsertLastSeenAsync(e.ProviderCardId, e.MarketPriceUsd, e.OccurredAt, ctx.CancellationToken);
        await repo.SaveChangesAsync(ctx.CancellationToken);
    }
}
