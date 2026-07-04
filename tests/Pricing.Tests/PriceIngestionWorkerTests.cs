using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Domain.Enums;
using TCGTrading.Pricing.Domain.ValueObjects;
using TCGTrading.Pricing.Infrastructure.Ingestion;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Pricing.Tests;

public class PriceIngestionWorkerTests
{
    private sealed class StubTcgdexPriceClient(decimal? price) : ITcgdexPriceClient
    {
        public Task<decimal?> GetMarketPriceUsdAsync(string providerCardId, CancellationToken ct)
            => Task.FromResult(price);
    }

    private sealed class StubWatchedCardRepository(IReadOnlyList<WatchedCard> cards) : IWatchedCardRepository
    {
        public Task<IReadOnlyList<WatchedCard>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(cards);

        public Task IncrementAsync(string providerCardId, Guid cardId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DecrementAsync(string providerCardId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakePriceRepository(decimal? latestPrice) : IPriceRepository
    {
        public List<PriceSnapshot> Snapshots { get; } = [];

        public Task AddAsync(PriceSnapshot snapshot, CancellationToken ct = default)
        {
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PriceBucket>> GetHistoryAsync(
            Guid cardId, DateTimeOffset from, DateTimeOffset to, Granularity granularity, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PriceBucket>>([]);

        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task<decimal?> GetLatestPriceAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult(latestPrice);
    }

    private static WatchedCard MakeWatchedCard() => new()
    {
        ProviderCardId = "base1-001",
        CardId = Guid.NewGuid(),
        RefCount = 1,
        UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task RunOnceAsync_WritesSnapshotAndPublishesEvent_WhenPriceChanged()
    {
        using var harness = new InMemoryTestHarness();
        await harness.Start();
        try
        {
            var watchedCard = MakeWatchedCard();
            var priceClient = new StubTcgdexPriceClient(9.00m);
            var watchedCardRepo = new StubWatchedCardRepository([watchedCard]);
            var priceRepo = new FakePriceRepository(latestPrice: 8.00m);
            var worker = new PriceIngestionWorker(
                new ConfigurationBuilder().Build(),
                NullLogger<PriceIngestionWorker>.Instance);

            await worker.RunOnceAsync(priceClient, watchedCardRepo, priceRepo, harness.Bus, CancellationToken.None);

            priceRepo.Snapshots.Should().ContainSingle(s => s.CardId == watchedCard.CardId && s.Price == 9.00m);
            (await harness.Published.Any<CardPriceUpdatedEvent>()).Should().BeTrue();

            var published = harness.Published.Select<CardPriceUpdatedEvent>().Single().Context.Message;
            published.CardId.Should().Be(watchedCard.CardId);
            published.ProviderCardId.Should().Be(watchedCard.ProviderCardId);
            published.MarketPriceUsd.Should().Be(9.00m);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task RunOnceAsync_DoesNothing_WhenPriceUnchanged()
    {
        using var harness = new InMemoryTestHarness();
        await harness.Start();
        try
        {
            var watchedCard = MakeWatchedCard();
            var priceClient = new StubTcgdexPriceClient(9.00m);
            var watchedCardRepo = new StubWatchedCardRepository([watchedCard]);
            var priceRepo = new FakePriceRepository(latestPrice: 9.00m);
            var worker = new PriceIngestionWorker(
                new ConfigurationBuilder().Build(),
                NullLogger<PriceIngestionWorker>.Instance);

            await worker.RunOnceAsync(priceClient, watchedCardRepo, priceRepo, harness.Bus, CancellationToken.None);

            priceRepo.Snapshots.Should().BeEmpty();
            (await harness.Published.Any<CardPriceUpdatedEvent>()).Should().BeFalse();
        }
        finally
        {
            await harness.Stop();
        }
    }
}
