using MassTransit;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Pricing.Infrastructure.Messaging;

public class PriceAlertCreatedConsumer(IWatchedCardRepository repo) : IConsumer<PriceAlertCreatedEvent>
{
    public async Task Consume(ConsumeContext<PriceAlertCreatedEvent> ctx)
    {
        // PriceAlertCreatedEvent doesn't carry CardId, so we increment with Guid.Empty;
        // CardId gets backfilled the next time CardPriceUpdatedEvent is published for this ProviderCardId.
        await repo.IncrementAsync(ctx.Message.ProviderCardId, Guid.Empty, ctx.CancellationToken);
    }
}
