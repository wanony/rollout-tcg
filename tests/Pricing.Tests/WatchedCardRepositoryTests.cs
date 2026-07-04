using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TCGTrading.Pricing.Infrastructure.Persistence;

namespace TCGTrading.Pricing.Tests;

public class WatchedCardRepositoryTests
{
    private static PricingOutboxDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PricingOutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PricingOutboxDbContext(options);
    }

    [Fact]
    public async Task IncrementAsync_CreatesRowAtRefCountOne_WhenAbsent()
    {
        await using var db = CreateContext();
        var repo = new WatchedCardRepository(db);
        var cardId = Guid.NewGuid();

        await repo.IncrementAsync("base1-001", cardId);

        var all = await repo.GetAllAsync();
        all.Should().ContainSingle(w => w.ProviderCardId == "base1-001" && w.RefCount == 1 && w.CardId == cardId);
    }

    [Fact]
    public async Task IncrementAsync_IncrementsExistingRow()
    {
        await using var db = CreateContext();
        var repo = new WatchedCardRepository(db);
        var cardId = Guid.NewGuid();

        await repo.IncrementAsync("base1-001", cardId);
        await repo.IncrementAsync("base1-001", cardId);

        var all = await repo.GetAllAsync();
        all.Should().ContainSingle(w => w.ProviderCardId == "base1-001" && w.RefCount == 2);
    }

    [Fact]
    public async Task DecrementAsync_DecrementsRefCount_WithoutRemoving_WhenAboveZero()
    {
        await using var db = CreateContext();
        var repo = new WatchedCardRepository(db);
        var cardId = Guid.NewGuid();

        await repo.IncrementAsync("base1-001", cardId);
        await repo.IncrementAsync("base1-001", cardId);
        await repo.DecrementAsync("base1-001");

        var all = await repo.GetAllAsync();
        all.Should().ContainSingle(w => w.ProviderCardId == "base1-001" && w.RefCount == 1);
    }

    [Fact]
    public async Task DecrementAsync_RemovesRow_WhenRefCountHitsZero()
    {
        await using var db = CreateContext();
        var repo = new WatchedCardRepository(db);
        var cardId = Guid.NewGuid();

        await repo.IncrementAsync("base1-001", cardId);
        await repo.DecrementAsync("base1-001");

        var all = await repo.GetAllAsync();
        all.Should().BeEmpty();
    }
}
