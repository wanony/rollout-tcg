using MassTransit;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Pricing.Infrastructure.Messaging;

public class PriceAlertDeletedConsumer(IWatchedCardRepository repo) : IConsumer<PriceAlertDeletedEvent>
{
    public async Task Consume(ConsumeContext<PriceAlertDeletedEvent> ctx)
    {
        await repo.DecrementAsync(ctx.Message.ProviderCardId, ctx.CancellationToken);
    }
}
