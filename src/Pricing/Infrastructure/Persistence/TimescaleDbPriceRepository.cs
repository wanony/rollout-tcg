using System.Data;
using Dapper;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Domain.Enums;
using TCGTrading.Pricing.Domain.ValueObjects;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public sealed class TimescaleDbPriceRepository(IDbConnection db) : IPriceRepository
{
    public async Task AddAsync(PriceSnapshot snapshot, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO price_snapshots (card_id, price, currency, source, recorded_at)
            VALUES (@CardId, @Price, @Currency, @Source, @RecordedAt)
            """;
        await db.ExecuteAsync(new CommandDefinition(sql, snapshot, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PriceBucket>> GetHistoryAsync(
        Guid cardId,
        DateTimeOffset from,
        DateTimeOffset to,
        Granularity granularity,
        CancellationToken ct = default)
    {
        var interval = granularity switch
        {
            Granularity.Hour => "1 hour",
            Granularity.Day  => "1 day",
            Granularity.Week => "1 week",
            _                => throw new ArgumentOutOfRangeException(nameof(granularity)),
        };

        const string sql = """
            SELECT
                time_bucket(@interval::interval, recorded_at) AS "BucketStart",
                first(price, recorded_at)                      AS "Open",
                max(price)                                     AS "High",
                min(price)                                     AS "Low",
                last(price, recorded_at)                       AS "Close",
                avg(price)                                     AS "Average"
            FROM price_snapshots
            WHERE card_id = @cardId
              AND recorded_at BETWEEN @from AND @to
            GROUP BY time_bucket(@interval::interval, recorded_at)
            ORDER BY "BucketStart" ASC
            """;

        var results = await db.QueryAsync<PriceBucket>(
            new CommandDefinition(
                sql,
                new { interval, cardId, from, to },
                cancellationToken: ct));

        return results.ToList();
    }

    public async Task<bool> HasAnyAsync(CancellationToken ct = default)
    {
        return await db.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM price_snapshots LIMIT 1)",
                cancellationToken: ct));
    }
}
