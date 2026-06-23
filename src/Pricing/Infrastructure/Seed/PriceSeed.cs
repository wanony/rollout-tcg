using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;

namespace TCGTrading.Pricing.Infrastructure.Seed;

public static class PriceSeed
{
    public static readonly Guid DragonFireId     = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid ShadowKnightId   = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid ForestGuardianId = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    public static readonly Guid LightningBoltId  = Guid.Parse("a1000000-0000-0000-0000-000000000004");
    public static readonly Guid AncientDragonId  = Guid.Parse("a1000000-0000-0000-0000-000000000005");

    private static readonly (Guid Id, decimal BasePrice)[] Cards =
    [
        (DragonFireId,     12.50m),
        (ShadowKnightId,    4.00m),
        (ForestGuardianId,  0.75m),
        (LightningBoltId,   1.25m),
        (AncientDragonId,  85.00m),
    ];

    public static async Task SeedAsync(
        IPriceRepository repo,
        CancellationToken ct = default)
    {
        var rng = new Random(42);
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        foreach (var (id, basePrice) in Cards)
        {
            var price = basePrice;
            for (var day = -29; day <= 0; day++)
            {
                var snapshot = PriceSnapshot.Create(
                    id,
                    Math.Round(price, 4),
                    "USD",
                    "seed",
                    today.AddDays(day));
                await repo.AddAsync(snapshot, ct);
                price *= 1m + (decimal)(rng.NextDouble() * 0.10 - 0.05);
                price = Math.Max(price, 0.01m);
            }
        }
    }
}
