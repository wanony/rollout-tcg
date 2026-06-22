using FluentAssertions;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.SharedKernel.Tests.Contracts;

public class IntegrationEventTests
{
    [Fact]
    public void CardPriceUpdatedEvent_PropertiesAccessible()
    {
        var id = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var @event = new CardPriceUpdatedEvent(
            EventId: id,
            OccurredAt: DateTime.UtcNow,
            CardId: cardId,
            ProviderCardId: "xy1-1",
            TcgProvider: "Pokemon",
            MarketPriceUsd: 25.50m,
            LowPriceUsd: 20.00m,
            HighPriceUsd: 30.00m);

        @event.EventId.Should().Be(id);
        @event.CardId.Should().Be(cardId);
        @event.MarketPriceUsd.Should().Be(25.50m);
        @event.TcgProvider.Should().Be("Pokemon");
    }

    [Fact]
    public void ListingPurchasedEvent_PropertiesAccessible()
    {
        var @event = new ListingPurchasedEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ListingId: Guid.NewGuid(),
            CardId: Guid.NewGuid(),
            BuyerId: Guid.NewGuid(),
            SellerId: Guid.NewGuid(),
            PurchasePriceUsd: 45.00m);

        @event.PurchasePriceUsd.Should().Be(45.00m);
    }

    [Fact]
    public void AllEvents_InheritIntegrationEvent()
    {
        var events = new IntegrationEvent[]
        {
            new CardPriceUpdatedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "x", "Pokemon", 1m, 1m, 1m),
            new ListingCreatedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "Pikachu", Guid.NewGuid(), 10m, "NearMint"),
            new ListingPurchasedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m),
            new OfferMadeEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 8m),
            new OfferAcceptedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 8m),
        };

        events.Should().AllBeAssignableTo<IntegrationEvent>();
    }
}
