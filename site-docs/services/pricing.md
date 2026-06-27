# Pricing Service

**Port:** 5006  
**Database:** `db_pricing` (TimescaleDB, port 5433)  
**Technology:** Dapper 2.x + Npgsql, TimescaleDB

The Pricing Service records and queries card price snapshots in TimescaleDB. It exposes a time-series API returning OHLC-bucketed data for chart rendering. Dapper is used instead of EF Core — the service relies heavily on TimescaleDB-specific SQL functions that have no EF Core translation.

## Database schema

```sql
CREATE TABLE price_snapshots (
    card_id     UUID         NOT NULL,
    price_usd   NUMERIC(10, 2) NOT NULL,
    recorded_at TIMESTAMPTZ  NOT NULL
);

-- TimescaleDB: auto-partition by time
SELECT create_hypertable('price_snapshots', 'recorded_at');
CREATE INDEX ON price_snapshots (card_id, recorded_at DESC);
```

The hypertable partitions data into "chunks" by time range (default 7 days). Queries over a time window only scan relevant chunks — performance stays consistent even as the table grows.

## Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/prices/{cardId}` | Record a new price snapshot |
| `GET` | `/prices/{cardId}/history` | Get OHLC price history |
| `GET` | `/health` | Health check |

### Record a price snapshot

```bash
curl -X POST http://localhost:5006/prices/base1-4 \
  -H "Content-Type: application/json" \
  -d '{ "priceUsd": 485.00 }'
```

### Get price history

```
GET /prices/{cardId}/history?granularity=Day&from=2025-01-01&to=2025-06-01
```

| Query param | Values | Description |
|---|---|---|
| `granularity` | `Hour`, `Day`, `Week` | Bucket size for aggregation |
| `from` | ISO 8601 datetime | Start of range |
| `to` | ISO 8601 datetime | End of range |

```json
[
  {
    "bucket": "2025-01-01T00:00:00Z",
    "open": 450.00,
    "high": 510.00,
    "low": 440.00,
    "close": 490.00
  }
]
```

## OHLC query

The chart data is computed entirely in SQL using TimescaleDB aggregate functions:

```sql
SELECT
    time_bucket(:interval, recorded_at) AS bucket,
    first(price_usd, recorded_at)       AS open,
    MAX(price_usd)                      AS high,
    MIN(price_usd)                      AS low,
    last(price_usd, recorded_at)        AS close
FROM price_snapshots
WHERE card_id = :cardId
  AND recorded_at BETWEEN :from AND :to
GROUP BY bucket
ORDER BY bucket;
```

`first()` and `last()` are TimescaleDB aggregate functions that return the value at the earliest and latest `recorded_at` within each bucket — giving true open and close prices rather than min/max order proxies.

## Seed data

At startup, the service seeds 150 synthetic price snapshots per card for a set of demo card IDs. This gives the chart page meaningful data without requiring real-time price polling:

| Card | pokemontcg.io ID |
|---|---|
| Dragon Fire | `a1000000-...0001` |
| Shadow Knight | `a1000000-...0002` |
| Forest Guardian | `a1000000-...0003` |
| Lightning Bolt | `a1000000-...0004` |
| Ancient Dragon | `a1000000-...0005` |

!!! note "Seed vs real prices"
    The seed data uses synthetic card IDs (not real pokemontcg.io IDs) and generates random walk prices. The card images on the Cards page come from pokemontcg.io and have their own TCGPlayer market prices. These are intentionally separate — the Pricing service models a *platform-internal* price history, not a TCGPlayer price mirror.

## Custom OTel activity source

The Pricing service adds a custom `ActivitySource` for tracing pricing operations:

```csharp
// Spans appear in Grafana Tempo under "pricing"
using var activity = _activitySource.StartActivity("record_price_snapshot");
activity?.SetTag("card.id", cardId);
activity?.SetTag("price.usd", priceUsd);
```

## Testing

`tests/Pricing.Tests/` uses `Testcontainers` with the `timescale/timescaledb:latest-pg16` image to run real TimescaleDB queries in tests. Tests cover snapshot insertion, OHLC bucketing at all three granularities, and the seed service.
