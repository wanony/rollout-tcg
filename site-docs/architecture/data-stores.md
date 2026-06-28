# Data Stores

Each service owns its data. No service reads another's database. The right tool is used for each data shape rather than forcing everything into one store.

## PostgreSQL 16 — relational data

Five services use PostgreSQL with EF Core 10:

| Database | Service | Key tables |
|---|---|---|
| `db_identity` | Identity | ASP.NET Identity tables (AspNetUsers, AspNetRoles, etc.) |
| `db_portfolio` | Portfolio | `CollectionItems` |
| `db_marketplace` | Marketplace | `Listings` |
| `db_notification` | Notification | `UserNotifications` |
| `db_cardcatalog` | Card Catalog | `Cards` |

All databases are created by `infra/postgres/init/01-create-databases.sql`, which runs once when the Postgres container is first initialised. EF Core migrations handle schema within each database — services call `db.Database.MigrateAsync()` at startup.

**Why PostgreSQL?** It's the standard production choice for relational data in .NET microservices. Npgsql has first-class EF Core support, JSON column support, and excellent performance characteristics. Using one Postgres instance (with separate databases) rather than five containers keeps the local dev footprint manageable.

## TimescaleDB — time-series price history

The Pricing service uses a separate `timescale/timescaledb:latest-pg16` container (port 5433) with a `db_pricing` database.

Price snapshots are stored in a **hypertable** — a TimescaleDB abstraction over a regular Postgres table that automatically partitions data by time. This makes range queries and aggregations over time windows dramatically faster than a plain table as data grows.

```sql
-- Created at startup via migration.sql
CREATE TABLE price_snapshots (
    card_id UUID NOT NULL,
    price_usd NUMERIC(10, 2) NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL
);

SELECT create_hypertable('price_snapshots', 'recorded_at');
CREATE INDEX ON price_snapshots (card_id, recorded_at DESC);
```

Chart data is returned as OHLC buckets using TimescaleDB aggregate functions:

```sql
SELECT
    time_bucket(:interval, recorded_at) AS bucket,
    first(price_usd, recorded_at)       AS open,
    MAX(price_usd)                      AS high,
    MIN(price_usd)                      AS low,
    last(price_usd, recorded_at)        AS close
FROM price_snapshots
WHERE card_id = :cardId
  AND recorded_at >= :from
GROUP BY bucket
ORDER BY bucket
```

**Why Dapper instead of EF Core here?** The `time_bucket()` and `first()`/`last()` aggregate functions are TimescaleDB extensions with no EF Core translation. Raw SQL via Dapper is the cleaner choice — no awkward `FromSqlRaw` workarounds.

## MeiliSearch — full-text card search

Card Catalog indexes every card into MeiliSearch for fast, typo-tolerant full-text search. Cards are written to PostgreSQL and indexed into MeiliSearch on each write. At startup, the service syncs all existing cards to ensure the index is consistent with the DB.

**Searchable attributes** (ranked by relevance weight):

1. `name`
2. `type`
3. `set`
4. `text` (card flavour text)

**Filterable attributes:** `set`, `rarity`, `type`

```json
// Example search result
{
  "hits": [
    { "id": "base1-4", "name": "Charizard", "type": "Fire", "rarity": "Rare Holo", "set": "Base Set" }
  ],
  "query": "charizard",
  "processingTimeMs": 1
}
```

**Why MeiliSearch over Postgres full-text search?** Postgres FTS requires exact prefix matches with some trigram support via `pg_trgm`. MeiliSearch provides typo tolerance, relevance ranking, and instant response times out of the box. It's self-hosted, MIT-licensed, and the SDK is well-maintained. For a card catalog where users spell card names wrong often, this matters.

## Redis 7 — rate limiting

The API Gateway uses Redis as a sliding window rate limit counter store via `RedisRateLimiting.AspNetCore`. Redis's atomic increment + TTL operations make it well-suited for rate limit counters at low latency.

Rate limit keys:
- Anonymous: `rl:anon:<ip>` — 60 requests / minute
- Authenticated: `rl:auth:<jwt-sub>` — 300 requests / minute

Redis is also available for future use as a distributed cache (session data, card search result caching).

## Storage topology in Docker Compose

```
postgres (5432)      → db_identity, db_portfolio, db_marketplace,
                        db_notification, db_cardcatalog
timescaledb (5433)   → db_pricing
meilisearch (7700)   → cards index
redis (6379)         → rate limit counters
```

Named Docker volumes persist data across container restarts:
`postgres_data`, `timescale_data`, `meilisearch_data`, `redis_data`
