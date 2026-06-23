# Price History — Design Spec

**Date:** 2026-06-23
**Status:** Approved
**Scope:** Pricing service gets a real domain model: time-series price snapshots stored in TimescaleDB, aggregated via `time_bucket()`, exposed via a chart-ready REST API.

---

## Goal

Replace the Pricing stub with a real service. Cards have price history stored as time-series data in TimescaleDB (separate container). A chart API returns OHLC-bucketed price data for any time range and granularity. Hirability signals: polyglot persistence, TimescaleDB hypertables, Dapper for raw SQL, contrast with CardCatalog's EF Core.

---

## Architecture

```
POST /prices/{cardId}     →  Pricing service  →  INSERT price_snapshots (TimescaleDB hypertable)
GET  /prices/{cardId}/history  →  Pricing service  →  time_bucket() SQL  →  PriceBucket[]
```

TimescaleDB runs as a **separate container** (`timescale/timescaledb:latest-pg16`, port 5433) with its own `db_pricing` database. All other services remain on the existing `postgres:16-alpine` container — zero risk to existing data.

Pricing service uses **Dapper** (not EF Core). Schema managed via a raw `migration.sql` embedded resource, applied at startup. Demonstrates picking the right tool: Dapper fits read-heavy time-series queries better than EF Core.

---

## Files

### New

```
src/Pricing/Domain/Entities/PriceSnapshot.cs
src/Pricing/Domain/Enums/Granularity.cs
src/Pricing/Domain/ValueObjects/PriceBucket.cs
src/Pricing/Application/Interfaces/IPriceRepository.cs
src/Pricing/Application/DTOs/RecordPriceRequest.cs
src/Pricing/Application/DTOs/PriceHistoryResponse.cs
src/Pricing/Infrastructure/Persistence/TimescaleDbPriceRepository.cs
src/Pricing/Infrastructure/Persistence/PriceHistoryExtensions.cs
src/Pricing/Infrastructure/Persistence/migration.sql
src/Pricing/Infrastructure/Seed/PriceSeed.cs
tests/Pricing.Tests/TCGTrading.Pricing.Api.Tests.csproj
tests/Pricing.Tests/test.runsettings
tests/Pricing.Tests/GlobalUsings.cs
tests/Pricing.Tests/Fixtures/PricingFixture.cs
tests/Pricing.Tests/Integration/PriceHistoryIntegrationTests.cs
```

### Modified

```
src/Pricing/TCGTrading.Pricing.Api.csproj     — add Dapper, Npgsql
src/Pricing/Program.cs                         — wire AddPriceHistory(), startup, endpoints
src/Pricing/appsettings.json                   — add ConnectionStrings:PricingDb
src/Pricing/appsettings.Development.json       — localhost:5433
docker-compose.yml                             — add timescaledb service + timescaledb_data volume
Directory.Packages.props                       — add Dapper, Npgsql versions
TCGTrading.sln                                 — add Pricing.Tests project
```

---

## Domain Model

### `Granularity` enum

```
Hour = 0
Day  = 1
Week = 2
```

Maps to TimescaleDB `time_bucket` intervals: `'1 hour'`, `'1 day'`, `'1 week'`.

### `PriceSnapshot` entity

| Field | Type | Notes |
|-------|------|-------|
| `CardId` | `Guid` | FK to card (no enforced constraint — cross-service) |
| `Price` | `decimal` | Stored as `NUMERIC(12,4)` |
| `Currency` | `string` | ISO 4217 code, default `"USD"`, max 3 chars |
| `Source` | `string` | e.g. `"manual"`, `"market-feed"`, `"seed"` |
| `RecordedAt` | `DateTimeOffset` | Hypertable partition key |

No surrogate `Id` — time-series rows are identified by `(card_id, recorded_at)`.

Static factory: `PriceSnapshot.Create(cardId, price, currency, source, recordedAt)`.

### `PriceBucket` value object

Aggregated result from a `time_bucket()` query:

| Field | Type |
|-------|------|
| `BucketStart` | `DateTimeOffset` |
| `Open` | `decimal` |
| `High` | `decimal` |
| `Low` | `decimal` |
| `Close` | `decimal` |
| `Average` | `decimal` |

---

## Application Layer

### `IPriceRepository`

```csharp
Task AddAsync(PriceSnapshot snapshot, CancellationToken ct = default);
Task<IReadOnlyList<PriceBucket>> GetHistoryAsync(
    Guid cardId,
    DateTimeOffset from,
    DateTimeOffset to,
    Granularity granularity,
    CancellationToken ct = default);
Task<bool> HasAnyAsync(CancellationToken ct = default);
```

### DTOs

**`RecordPriceRequest`** (request body for `POST /prices/{cardId}`):
```csharp
public sealed record RecordPriceRequest(decimal Price, string Currency, string Source);
```
Defaults: `Currency = "USD"`, `Source = "manual"` (applied in endpoint if null/empty).

**`PriceHistoryResponse`** (response for `GET /prices/{cardId}/history`):
```csharp
public sealed record PriceHistoryResponse(
    Guid CardId,
    string Granularity,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<PriceBucket> Buckets);
```

---

## Database Schema

File: `src/Pricing/Infrastructure/Persistence/migration.sql` (embedded resource)

```sql
CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;

CREATE TABLE IF NOT EXISTS price_snapshots (
    card_id     UUID          NOT NULL,
    price       NUMERIC(12,4) NOT NULL,
    currency    VARCHAR(3)    NOT NULL DEFAULT 'USD',
    source      VARCHAR(50)   NOT NULL,
    recorded_at TIMESTAMPTZ   NOT NULL
);

SELECT create_hypertable(
    'price_snapshots', 'recorded_at',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS ix_price_snapshots_card
    ON price_snapshots(card_id, recorded_at DESC);
```

Applied at startup via `connection.ExecuteAsync(sql)` using Dapper. Idempotent (`IF NOT EXISTS` throughout).

---

## Infrastructure

### `TimescaleDbPriceRepository`

**AddAsync:** plain `INSERT INTO price_snapshots (card_id, price, currency, source, recorded_at) VALUES (...)`.

**GetHistoryAsync:** uses `time_bucket()` with `first()` / `last()` aggregate functions (TimescaleDB hyperfunctions — require TimescaleDB extension):

```sql
SELECT
    time_bucket(@interval::interval, recorded_at) AS bucket_start,
    first(price, recorded_at)                      AS open,
    max(price)                                     AS high,
    min(price)                                     AS low,
    last(price, recorded_at)                       AS close,
    avg(price)                                     AS average
FROM price_snapshots
WHERE card_id = @cardId
  AND recorded_at BETWEEN @from AND @to
GROUP BY bucket_start
ORDER BY bucket_start ASC
```

`@interval` parameter maps from `Granularity` enum: `Hour → "1 hour"`, `Day → "1 day"`, `Week → "1 week"`.

**HasAnyAsync:** `SELECT EXISTS (SELECT 1 FROM price_snapshots LIMIT 1)`.

### `PriceHistoryExtensions`

```csharp
public static IHostApplicationBuilder AddPriceHistory(this IHostApplicationBuilder builder)
```

Registers:
- `IDbConnection` factory (Npgsql) using `ConnectionStrings:PricingDb` — throws `InvalidOperationException` if null
- `IPriceRepository` → `TimescaleDbPriceRepository` (scoped)

### `IDbConnection` registration

Use a factory that opens a new `NpgsqlConnection` per scope:
```csharp
services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(connectionString));
```

---

## API Endpoints

### `POST /prices/{cardId}`

Records a new price snapshot.

- Body: `RecordPriceRequest { Price, Currency?, Source? }`
- Defaults: `Currency = "USD"` if null/empty; `Source = "manual"` if null/empty
- Response: `201 Created` with the created snapshot
- Returns `400 Bad Request` if `Price <= 0`

### `GET /prices/{cardId}/history`

Returns bucketed price history.

Query parameters:
| Param | Type | Default | Notes |
|-------|------|---------|-------|
| `from` | `DateTimeOffset?` | `now - 30 days` | inclusive |
| `to` | `DateTimeOffset?` | `now` | inclusive |
| `granularity` | `string` | `"day"` | `"hour"`, `"day"`, `"week"` |

Response `200 OK`:
```json
{
  "cardId": "...",
  "granularity": "day",
  "from": "2026-05-24T00:00:00Z",
  "to": "2026-06-23T00:00:00Z",
  "buckets": [
    {
      "bucketStart": "2026-05-24T00:00:00Z",
      "open": 12.5000,
      "high": 13.2000,
      "low": 11.8000,
      "close": 12.9000,
      "average": 12.6000
    }
  ]
}
```

Returns `400 Bad Request` if `granularity` value is not one of `hour`, `day`, `week`.
Returns `200 OK` with empty `buckets` array if no data exists for the card/range.

---

## Startup Sequence

`Program.cs` after `app.Build()`:

1. Apply `migration.sql` via Dapper (`CREATE EXTENSION`, table, hypertable, index — all idempotent)
2. Check `HasAnyAsync()` — if table empty, run `PriceSeed.SeedAsync(repo)`
3. Register endpoints
4. `app.Run()`

---

## Seed Data

`PriceSeed.SeedAsync(IPriceRepository repo, CancellationToken ct)` generates synthetic price history:

- 5 demo cards with **fixed, hardcoded GUIDs** defined as constants in `PriceSeed`
- 30 days of daily snapshots per card (150 rows total)
- Random walk: start price per card, each day ±5% of previous day's price
- `source = "seed"`, `currency = "USD"`
- `RecordedAt` from `DateTimeOffset.UtcNow.AddDays(-29)` to `DateTimeOffset.UtcNow` (one per day at midnight UTC)

Base prices and demo GUIDs (hardcoded in `PriceSeed`):

| Card | Demo GUID | Base Price |
|------|-----------|-----------|
| Dragon Fire | `a1000000-0000-0000-0000-000000000001` | 12.50 |
| Shadow Knight | `a1000000-0000-0000-0000-000000000002` | 4.00 |
| Forest Guardian | `a1000000-0000-0000-0000-000000000003` | 0.75 |
| Lightning Bolt | `a1000000-0000-0000-0000-000000000004` | 1.25 |
| Ancient Dragon | `a1000000-0000-0000-0000-000000000005` | 85.00 |

Note: CardCatalog generates card IDs at runtime via `Guid.NewGuid()` — they do not match these demo GUIDs. Pricing is an independent service; `POST /prices/{cardId}` accepts any UUID. The demo GUIDs are standalone identifiers for the chart demo. In a production system, card IDs would flow from CardCatalog via events.

---

## Configuration

`appsettings.json`:
```json
{
  "ConnectionStrings": {
    "PricingDb": "Host=timescaledb;Port=5432;Database=db_pricing;Username=tcg;Password=dev"
  }
}
```

`appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "PricingDb": "Host=localhost;Port=5433;Database=db_pricing;Username=tcg;Password=dev"
  }
}
```

---

## Docker Compose

Add to `docker-compose.yml`:

```yaml
  timescaledb:
    image: timescale/timescaledb:latest-pg16
    environment:
      POSTGRES_USER: tcg
      POSTGRES_PASSWORD: dev
      POSTGRES_DB: db_pricing
    ports:
      - "5433:5432"
    volumes:
      - timescaledb_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tcg"]
      interval: 5s
      timeout: 3s
      retries: 5
```

Add `timescaledb_data:` to the `volumes:` block.

Pricing service `depends_on: timescaledb: condition: service_healthy`.

---

## NuGet Packages

Add to `Directory.Packages.props`:
```xml
<PackageVersion Include="Dapper" Version="2.1.35" />
<PackageVersion Include="Npgsql" Version="8.0.0" />
```

Note: `Npgsql.EntityFrameworkCore.PostgreSQL` already in `Directory.Packages.props` — `Npgsql` base package must be added separately for Dapper use.

Add to `src/Pricing/TCGTrading.Pricing.Api.csproj`:
```xml
<PackageReference Include="Dapper" />
<PackageReference Include="Npgsql" />
```

---

## Tests

Project: `tests/Pricing.Tests/`
Target framework: `net10.0`
Fixture: `WebApplicationFactory<Program>` + `PostgreSqlBuilder().WithImage("timescale/timescaledb:latest-pg16")` from `Testcontainers.PostgreSql`.

The fixture overrides `ConnectionStrings:PricingDb` via `ConfigureAppConfiguration`.

**Test 1 — Record and retrieve:**
POST a price snapshot for a card → GET history → `buckets` contains a bucket with `close` matching posted price.

**Test 2 — Daily granularity buckets hourly data:**
Use a fresh `cardId = Guid.NewGuid()` (not in seed). Insert 24 price rows via `IPriceRepository` (obtained from `fixture.Services.CreateScope().ServiceProvider`) with `recorded_at` spread one-per-hour across a single UTC day → GET `/prices/{cardId}/history?granularity=day&from=<day>&to=<day+1>` → response has exactly 1 bucket.

Using a unique card ID avoids overlap with the 30-day seed data and makes the assertion unambiguous.

**Test 3 — Time range filter:**
Use a fresh `cardId = Guid.NewGuid()`. Insert 10 daily snapshots (days `-10` to `-1` relative to now) via `IPriceRepository` → GET with `from=now-3days&to=now&granularity=day` → exactly 3 buckets returned.

---

## Clean Architecture Compliance

| Layer | Contents |
|-------|----------|
| Domain | `PriceSnapshot`, `PriceBucket`, `Granularity` — zero infrastructure imports |
| Application | `IPriceRepository`, DTOs — zero `Dapper`/`Npgsql` imports |
| Infrastructure | `TimescaleDbPriceRepository`, `PriceHistoryExtensions`, `migration.sql` |
| Presentation | `Program.cs` — calls interfaces only |

---

## Out of Scope

- Real market data feed integration — seed data only for now
- Multi-currency conversion — prices stored as-is; USD assumed
- Card price alerts / push notifications — future wishlist feature
- OHLC chart frontend — API returns data; rendering is client responsibility
- Retention policy / data downsampling — TimescaleDB supports this but out of scope for MVP
- Shared card ID constants between services via SharedKernel — cross-service coupling; hardcoded GUIDs in seed acceptable for demo
