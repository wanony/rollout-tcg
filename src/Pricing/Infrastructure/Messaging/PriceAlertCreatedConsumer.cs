using MassTransit;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Pricing.Infrastructure.Messaging;

public class PriceAlertCreatedConsumer(IWatchedCardRepository repo) : IConsumer<PriceAlertCreatedEvent>
{
    public async Task Consume(ConsumeContext<PriceAlertCreatedEvent> ctx)
    {
        // ponytail: CardId is unknown from PriceAlertCreatedEvent, so it stays Guid.Empty for alert-created rows.
        // The ingestion worker polls by ProviderCardId, so CardId isn't needed here; add a backfill consumer if a real CardId is ever required.
        await repo.IncrementAsync(ctx.Message.ProviderCardId, Guid.Empty, ctx.CancellationToken);
    }
}
