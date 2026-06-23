# Price History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Pricing service stub with a real time-series price history API backed by a dedicated TimescaleDB container, using Dapper for data access and exposing OHLC-bucketed chart data.

**Architecture:** Separate `timescale/timescaledb:latest-pg16` container on port 5433 holds `db_pricing`. Pricing service applies an embedded `migration.sql` at startup (creates extension + hypertable + index), seeds 150 rows of synthetic price data, then serves `POST /prices/{cardId}` and `GET /prices/{cardId}/history`. All data access goes through `IPriceRepository` → `TimescaleDbPriceRepository` using Dapper raw SQL with `time_bucket()` and TimescaleDB `first()`/`last()` aggregate functions.

**Tech Stack:** .NET 10, Dapper 2.1.35, Npgsql 8.0.0, TimescaleDB (PostgreSQL 16 extension), xUnit, Testcontainers.PostgreSql, FluentAssertions, WebApplicationFactory

## Global Constraints

- Target framework: `net10.0` for all new files; test project upgraded from `net8.0` to `net10.0`
- Clean architecture: Domain and Application layers import nothing from Infrastructure; `IPriceRepository` lives in Application; `TimescaleDbPriceRepository` lives in Infrastructure
- Dapper only — no EF Core in Pricing service
- TimescaleDB container image: `timescale/timescaledb:latest-pg16`
- TimescaleDB port: `5433` (host) → `5432` (container); production connection: `Host=timescaledb;Port=5432;Database=db_pricing;Username=tcg;Password=dev`
- Test DB connection: overridden via `ConfigureAppConfiguration` in `PricingFixture` using `PostgreSqlBuilder().WithImage("timescale/timescaledb:latest-pg16").GetConnectionString()`
- Granularity enum values: `Hour=0`, `Day=1`, `Week=2`
- Demo seed GUIDs — Dragon Fire: `a1000000-0000-0000-0000-000000000001`, Shadow Knight: `a1000000-0000-0000-0000-000000000002`, Forest Guardian: `a1000000-0000-0000-0000-000000000003`, Lightning Bolt: `a1000000-0000-0000-0000-000000000004`, Ancient Dragon: `a1000000-0000-0000-0000-000000000005`
- Seed base prices: Dragon Fire 12.50m, Shadow Knight 4.00m, Forest Guardian 0.75m, Lightning Bolt 1.25m, Ancient Dragon 85.00m
- Seed: 30 daily snapshots per card (150 rows), random walk ±5%, source="seed", currency="USD", deterministic RNG seed=42
- `migration.sql` embedded resource name: `TCGTrading.Pricing.Api.Infrastructure.Persistence.migration.sql`
- Embedded resource name derives from assembly name `TCGTrading.Pricing.Api` + relative path `Infrastructure.Persistence.migration.sql`
- Test project: `tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj` (already in solution — do NOT re-add to sln)
- Test namespace: `TCGTrading.Pricing.Tests`

---

## File Map

```
src/Pricing/
  Domain/
    Enums/
      Granularity.cs                        [CREATE Task 2]
    Entities/
      PriceSnapshot.cs                      [CREATE Task 2]
    ValueObjects/
      PriceBucket.cs                        [CREATE Task 2]
  Application/
    Interfaces/
      IPricingTelemetry.cs                  [EXISTING — do not touch]
      IPriceRepository.cs                   [CREATE Task 2]
    DTOs/
      RecordPriceRequest.cs                 [CREATE Task 2]
      PriceHistoryResponse.cs               [CREATE Task 2]
  Infrastructure/
    Telemetry/
      PricingTelemetry.cs                   [EXISTING — do not touch]
    Persistence/
      migration.sql                         [CREATE Task 3]
      PricingMigrationService.cs            [CREATE Task 3]
      TimescaleDbPriceRepository.cs         [CREATE Task 3]
      PriceHistoryExtensions.cs             [CREATE Task 3]
    Seed/
      PriceSeed.cs                          [CREATE Task 3]
  Program.cs                                [MODIFY Task 4 (skeleton), MODIFY Task 5 (final)]
  appsettings.json                          [MODIFY Task 1]
  appsettings.Development.json              [MODIFY Task 1]
  TCGTrading.Pricing.Api.csproj             [MODIFY Task 1 (packages), Task 3 (EmbeddedResource)]

Directory.Packages.props                    [MODIFY Task 1]
docker-compose.yml                          [MODIFY Task 1]

tests/Pricing.Tests/
  TCGTrading.Pricing.Tests.csproj           [MODIFY Task 4]
  Fixtures/
    PricingFixture.cs                       [CREATE Task 4]
  Integration/
    PriceHistoryIntegrationTests.cs         [CREATE Task 4]
```

---

### Task 1: Packages, Docker, and Config

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Pricing/TCGTrading.Pricing.Api.csproj`
- Modify: `docker-compose.yml`
- Modify: `src/Pricing/appsettings.json`
- Modify: `src/Pricing/appsettings.Development.json`

**Interfaces:**
- Produces: `Dapper` and `Npgsql` available in Pricing project; `timescaledb` container definition; `ConnectionStrings:PricingDb` config key available in appsettings

- [ ] **Step 1: Add Dapper and Npgsql to central package versions**

Open `Directory.Packages.props`. After the `<!-- Search -->` comment block, add two new entries:

```xml
    <!-- Pricing -->
    <PackageVersion Include="Dapper" Version="2.1.35" />
    <PackageVersion Include="Npgsql" Version="8.0.0" />
```

The full `ItemGroup` block at the end should look like:
```xml
    <!-- Search -->
    <PackageVersion Include="MeiliSearch" Version="0.19.0" />
    <!-- Testcontainers base (generic container API for MeiliSearch in tests) -->
    <PackageVersion Include="Testcontainers" Version="3.10.0" />
    <!-- Pricing -->
    <PackageVersion Include="Dapper" Version="2.1.35" />
    <PackageVersion Include="Npgsql" Version="8.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add package references to Pricing project**

Open `src/Pricing/TCGTrading.Pricing.Api.csproj`. Replace its entire content with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../SharedKernel/TCGTrading.SharedKernel.csproj" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
    <PackageReference Include="Dapper" />
    <PackageReference Include="Npgsql" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add timescaledb service to docker-compose**

Open `docker-compose.yml`. After the `meilisearch:` service block (ends at line 58), insert the `timescaledb` service. Place it before `identity-service:`. The new block is:

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

Also update the `volumes:` block at the bottom of the file to add `timescaledb_data:`:

```yaml
volumes:
  postgres_data:
  rabbitmq_data:
  tempo_data:
  prometheus_data:
  loki_data:
  grafana_data:
  meilisearch_data:
  timescaledb_data:
```

- [ ] **Step 4: Add PricingDb connection string to appsettings**

Replace `src/Pricing/appsettings.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Telemetry": {
    "OtlpEndpoint": "http://otel-collector:4317",
    "LokiEndpoint": "http://loki:3100"
  },
  "ConnectionStrings": {
    "PricingDb": "Host=timescaledb;Port=5432;Database=db_pricing;Username=tcg;Password=dev"
  }
}
```

Replace `src/Pricing/appsettings.Development.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Telemetry": {
    "OtlpEndpoint": "http://localhost:4317",
    "LokiEndpoint": "http://localhost:3100"
  },
  "ConnectionStrings": {
    "PricingDb": "Host=localhost;Port=5433;Database=db_pricing;Username=tcg;Password=dev"
  }
}
```

- [ ] **Step 5: Verify packages restore**

```bash
dotnet restore src/Pricing/TCGTrading.Pricing.Api.csproj
```

Expected: no errors, `Dapper 2.1.35` and `Npgsql 8.0.0` appear in the restore output.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/Pricing/TCGTrading.Pricing.Api.csproj docker-compose.yml src/Pricing/appsettings.json src/Pricing/appsettings.Development.json
git commit -m "chore: add Dapper/Npgsql packages, timescaledb container, pricing config"
```

---

### Task 2: Domain and Application Layer

**Files:**
- Create: `src/Pricing/Domain/Enums/Granularity.cs`
- Create: `src/Pricing/Domain/Entities/PriceSnapshot.cs`
- Create: `src/Pricing/Domain/ValueObjects/PriceBucket.cs`
- Create: `src/Pricing/Application/Interfaces/IPriceRepository.cs`
- Create: `src/Pricing/Application/DTOs/RecordPriceRequest.cs`
- Create: `src/Pricing/Application/DTOs/PriceHistoryResponse.cs`

**Interfaces:**
- Produces: `Granularity` enum, `PriceSnapshot.Create(...)` factory, `PriceBucket` class, `IPriceRepository` interface, `RecordPriceRequest` record, `PriceHistoryResponse` record — all consumed by Tasks 3, 4, 5

- [ ] **Step 1: Create Granularity enum**

Create `src/Pricing/Domain/Enums/Granularity.cs`:

```csharp
namespace TCGTrading.Pricing.Domain.Enums;

public enum Granularity
{
    Hour = 0,
    Day  = 1,
    Week = 2,
}
```

- [ ] **Step 2: Create PriceSnapshot entity**

Create `src/Pricing/Domain/Entities/PriceSnapshot.cs`:

```csharp
namespace TCGTrading.Pricing.Domain.Entities;

public sealed class PriceSnapshot
{
    public Guid CardId { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }

    private PriceSnapshot() { }

    public static PriceSnapshot Create(
        Guid cardId,
        decimal price,
        string currency,
        string source,
        DateTimeOffset recordedAt)
    {
        return new PriceSnapshot
        {
            CardId = cardId,
            Price = price,
            Currency = currency,
            Source = source,
            RecordedAt = recordedAt,
        };
    }
}
```

- [ ] **Step 3: Create PriceBucket value object**

Create `src/Pricing/Domain/ValueObjects/PriceBucket.cs`:

```csharp
namespace TCGTrading.Pricing.Domain.ValueObjects;

public sealed class PriceBucket
{
    public DateTimeOffset BucketStart { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Average { get; init; }
}
```

`init` setters let Dapper hydrate via property mapping while keeping the type immutable after construction.

- [ ] **Step 4: Create IPriceRepository interface**

Create `src/Pricing/Application/Interfaces/IPriceRepository.cs`:

```csharp
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Domain.Enums;
using TCGTrading.Pricing.Domain.ValueObjects;

namespace TCGTrading.Pricing.Application.Interfaces;

public interface IPriceRepository
{
    Task AddAsync(PriceSnapshot snapshot, CancellationToken ct = default);

    Task<IReadOnlyList<PriceBucket>> GetHistoryAsync(
        Guid cardId,
        DateTimeOffset from,
        DateTimeOffset to,
        Granularity granularity,
        CancellationToken ct = default);

    Task<bool> HasAnyAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Create DTOs**

Create `src/Pricing/Application/DTOs/RecordPriceRequest.cs`:

```csharp
namespace TCGTrading.Pricing.Application.DTOs;

public sealed record RecordPriceRequest(
    decimal Price,
    string? Currency = null,
    string? Source = null);
```

Create `src/Pricing/Application/DTOs/PriceHistoryResponse.cs`:

```csharp
using TCGTrading.Pricing.Domain.ValueObjects;

namespace TCGTrading.Pricing.Application.DTOs;

public sealed record PriceHistoryResponse(
    Guid CardId,
    string Granularity,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<PriceBucket> Buckets);
```

- [ ] **Step 6: Build to verify no compilation errors**

```bash
dotnet build src/Pricing/TCGTrading.Pricing.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`. The domain and application files compile cleanly against each other with no infrastructure imports.

- [ ] **Step 7: Commit**

```bash
git add src/Pricing/Domain/ src/Pricing/Application/Interfaces/IPriceRepository.cs src/Pricing/Application/DTOs/
git commit -m "feat(pricing): add domain entities and application interfaces for price history"
```

---

### Task 3: Infrastructure — Repository, Migration, Seed

**Files:**
- Create: `src/Pricing/Infrastructure/Persistence/migration.sql`
- Create: `src/Pricing/Infrastructure/Persistence/PricingMigrationService.cs`
- Create: `src/Pricing/Infrastructure/Persistence/TimescaleDbPriceRepository.cs`
- Create: `src/Pricing/Infrastructure/Persistence/PriceHistoryExtensions.cs`
- Create: `src/Pricing/Infrastructure/Seed/PriceSeed.cs`
- Modify: `src/Pricing/TCGTrading.Pricing.Api.csproj` — add `EmbeddedResource` for migration.sql

**Interfaces:**
- Consumes: `IPriceRepository`, `PriceSnapshot`, `PriceBucket`, `Granularity` from Task 2
- Produces:
  - `PriceHistoryExtensions.AddPriceHistory(this IHostApplicationBuilder)` — registers `IDbConnection` (scoped Npgsql connection), `IPriceRepository` → `TimescaleDbPriceRepository` (scoped), `PricingMigrationService` (scoped)
  - `PricingMigrationService.ApplyAsync(CancellationToken)` — runs embedded migration.sql
  - `PriceSeed.SeedAsync(IPriceRepository, CancellationToken)` — inserts 150 seed rows
  - `TimescaleDbPriceRepository : IPriceRepository`

- [ ] **Step 1: Create migration SQL**

Create `src/Pricing/Infrastructure/Persistence/migration.sql`:

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
    'price_snapshots',
    'recorded_at',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS ix_price_snapshots_card
    ON price_snapshots(card_id, recorded_at DESC);
```

- [ ] **Step 2: Register migration.sql as embedded resource**

Open `src/Pricing/TCGTrading.Pricing.Api.csproj`. Add an `ItemGroup` for the embedded resource:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../SharedKernel/TCGTrading.SharedKernel.csproj" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
    <PackageReference Include="Dapper" />
    <PackageReference Include="Npgsql" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="Infrastructure/Persistence/migration.sql" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create PricingMigrationService**

Create `src/Pricing/Infrastructure/Persistence/PricingMigrationService.cs`:

```csharp
using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public sealed class PricingMigrationService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("PricingDb")
        ?? throw new InvalidOperationException("ConnectionStrings:PricingDb is not configured.");

    public async Task ApplyAsync(CancellationToken ct = default)
    {
        var assembly = typeof(PricingMigrationService).Assembly;
        const string resourceName =
            "TCGTrading.Pricing.Api.Infrastructure.Persistence.migration.sql";

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(ct);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }
}
```

`PricingMigrationService` creates its own `NpgsqlConnection` (not the scoped `IDbConnection`) so the migration connection is isolated from the repository connection during startup.

- [ ] **Step 4: Create TimescaleDbPriceRepository**

Create `src/Pricing/Infrastructure/Persistence/TimescaleDbPriceRepository.cs`:

```csharp
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
```

**Notes:**
- `@interval::interval` — Npgsql sends `interval` as a text parameter; PostgreSQL casts it to the `interval` type. This is idiomatic for parameterized interval values.
- `GROUP BY time_bucket(...)` — repeating the full expression because PostgreSQL does not allow SELECT aliases in GROUP BY.
- `ORDER BY "BucketStart"` — PostgreSQL allows aliases in ORDER BY (evaluated after SELECT).
- `first()` and `last()` are TimescaleDB aggregate functions (not standard PostgreSQL). They require the `timescaledb` extension, which the migration enables.
- Dapper passes `PriceSnapshot` entity properties as SQL parameters: `@CardId`, `@Price`, `@Currency`, `@Source`, `@RecordedAt` — Dapper reads public properties for parameter binding.
- `QueryAsync<PriceBucket>` maps column `"BucketStart"` → `PriceBucket.BucketStart` etc. via case-insensitive property name matching. `init` setters are supported by Dapper 2.1+.

- [ ] **Step 5: Create PriceHistoryExtensions**

Create `src/Pricing/Infrastructure/Persistence/PriceHistoryExtensions.cs`:

```csharp
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TCGTrading.Pricing.Application.Interfaces;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public static class PriceHistoryExtensions
{
    public static IHostApplicationBuilder AddPriceHistory(
        this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("PricingDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PricingDb is not configured.");

        builder.Services.AddScoped<IDbConnection>(
            _ => new NpgsqlConnection(connectionString));

        builder.Services.AddScoped<IPriceRepository, TimescaleDbPriceRepository>();
        builder.Services.AddScoped<PricingMigrationService>();

        return builder;
    }
}
```

- [ ] **Step 6: Create PriceSeed**

Create `src/Pricing/Infrastructure/Seed/PriceSeed.cs`:

```csharp
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
```

- [ ] **Step 7: Build to verify**

```bash
dotnet build src/Pricing/TCGTrading.Pricing.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add src/Pricing/Infrastructure/ src/Pricing/TCGTrading.Pricing.Api.csproj
git commit -m "feat(pricing): add TimescaleDB repository, migration, seed, and DI extensions"
```

---

### Task 4: Skeleton Program.cs + Failing Integration Tests

**Goal:** Wire up infrastructure in `Program.cs` (startup + DI, no endpoints yet). Add integration tests. All 3 tests should compile and fail with 404 (endpoint does not exist yet).

**Files:**
- Modify: `src/Pricing/Program.cs` — add `AddPriceHistory()`, startup sequence (migration + seed), `public partial class Program {}`, no price endpoints
- Modify: `tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj` — upgrade to `net10.0`, add `Microsoft.AspNetCore.Mvc.Testing` and `Testcontainers.PostgreSql`
- Create: `tests/Pricing.Tests/Fixtures/PricingFixture.cs`
- Create: `tests/Pricing.Tests/Integration/PriceHistoryIntegrationTests.cs`

**Interfaces:**
- Consumes: `AddPriceHistory()` from Task 3, `PricingMigrationService` from Task 3, `IPriceRepository` from Task 2, `PriceSeed.SeedAsync` from Task 3, `PriceHistoryResponse` from Task 2, `PriceBucket` from Task 2
- Produces: `public partial class Program {}` (required for `WebApplicationFactory<Program>`)

- [ ] **Step 1: Write skeleton Program.cs**

Replace `src/Pricing/Program.cs` with:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Infrastructure.Persistence;
using TCGTrading.Pricing.Infrastructure.Seed;
using TCGTrading.Pricing.Infrastructure.Telemetry;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("pricing");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddSource("tcgtrading.pricing"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddSingleton<IPricingTelemetry, PricingTelemetry>();
builder.AddPriceHistory();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<PricingMigrationService>();
    await migrator.ApplyAsync();

    var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
    if (!await repo.HasAnyAsync())
        await PriceSeed.SeedAsync(repo);
}

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
```

**Important:** Keep the original telemetry configuration exactly. `AddTelemetry("pricing")` is an extension method from `TCGTrading.SharedKernel.Infrastructure.Telemetry` — that namespace is already in the using above.

- [ ] **Step 2: Build skeleton to confirm it compiles**

```bash
dotnet build src/Pricing/TCGTrading.Pricing.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Update test project**

Replace `tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RunSettingsFilePath>$(MSBuildProjectDirectory)/test.runsettings</RunSettingsFilePath>
    <NuGetAuditMode>direct</NuGetAuditMode>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <ProjectReference Include="../../src/Pricing/TCGTrading.Pricing.Api.csproj" />
  </ItemGroup>
</Project>
```

The test project is already registered in `TCGTrading.sln` — do NOT run `dotnet sln add`.

- [ ] **Step 4: Create PricingFixture**

Create `tests/Pricing.Tests/Fixtures/PricingFixture.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace TCGTrading.Pricing.Tests.Fixtures;

public class PricingFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _timescaledb = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:latest-pg16")
        .WithDatabase("db_pricing")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    public async Task InitializeAsync() => await _timescaledb.StartAsync();

    public new async Task DisposeAsync()
    {
        await _timescaledb.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PricingDb"] = _timescaledb.GetConnectionString(),
            });
        });
    }
}
```

`PostgreSqlBuilder` works with the TimescaleDB image because TimescaleDB IS PostgreSQL. `GetConnectionString()` returns a standard Npgsql connection string pointing to the test container.

- [ ] **Step 5: Write 3 failing integration tests**

Create `tests/Pricing.Tests/Integration/PriceHistoryIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TCGTrading.Pricing.Application.DTOs;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Tests.Fixtures;

namespace TCGTrading.Pricing.Tests.Integration;

public class PriceHistoryIntegrationTests(PricingFixture fixture)
    : IClassFixture<PricingFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task RecordPrice_ThenGetHistory_ReturnsBucketWithPostedPrice()
    {
        var cardId = Guid.NewGuid();
        const decimal postedPrice = 42.00m;

        var postResponse = await _client.PostAsJsonAsync(
            $"/prices/{cardId}",
            new RecordPriceRequest(postedPrice));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.GetAsync(
            $"/prices/{cardId}/history?granularity=day");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        body.Should().NotBeNull();
        body!.Buckets.Should().HaveCount(1);
        body.Buckets[0].Close.Should().Be(postedPrice);
    }

    [Fact]
    public async Task HourlyDataBucketedDaily_Returns_OneBucket()
    {
        var cardId = Guid.NewGuid();
        var day = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        await using var scope = fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        for (var h = 0; h < 24; h++)
        {
            await repo.AddAsync(PriceSnapshot.Create(
                cardId, 10m + h, "USD", "test", day.AddHours(h)));
        }

        var from = day.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var to   = day.AddDays(1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var response = await _client.GetAsync(
            $"/prices/{cardId}/history?granularity=day&from={from}&to={to}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        body.Should().NotBeNull();
        body!.Buckets.Should().HaveCount(1);
    }

    [Fact]
    public async Task TimeRangeFilter_Returns_OnlyBucketsInRange()
    {
        var cardId = Guid.NewGuid();
        var today  = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        await using var scope = fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        for (var i = 10; i >= 1; i--)
        {
            await repo.AddAsync(PriceSnapshot.Create(
                cardId, 10m + i, "USD", "test", today.AddDays(-i)));
        }

        var from = today.AddDays(-3).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var to   = today.AddDays(1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var response = await _client.GetAsync(
            $"/prices/{cardId}/history?granularity=day&from={from}&to={to}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        body.Should().NotBeNull();
        body!.Buckets.Should().HaveCount(3,
            "only the 3 records at days -3, -2, -1 fall within the query range");
    }
}
```

**Test design notes:**
- All 3 tests use `Guid.NewGuid()` for `cardId` to avoid interference with the 150-row seed data inserted at startup.
- Tests 2 and 3 insert via `IPriceRepository` directly (no HTTP) to test the bucketing logic without coupling to the write endpoint.
- `CreateAsyncScope()` is the .NET 6+ async-safe scope API — the `IDbConnection` registered as scoped is disposed when the scope exits.
- Timestamps use `"yyyy-MM-ddTHH:mm:ssZ"` — ASP.NET Core's `DateTimeOffset?` query binding handles this ISO 8601 format.
- Test 3 queries `from=today-3days to=today+1day` against 10 records at days -10 through -1. Records at days -3, -2, -1 fall within the range → 3 daily buckets.

- [ ] **Step 6: Build the test project to verify compilation**

```bash
dotnet build tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 7: Run tests — expect 3 failures (404)**

```bash
dotnet test tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj --logger "console;verbosity=normal" --filter "FullyQualifiedName~PriceHistoryIntegrationTests"
```

Expected: 3 tests fail. Failures should mention `404 Not Found` or `Expected Created but found NotFound` — confirming the tests are wired correctly; the endpoints just don't exist yet. The existing `PricingTelemetryTests` (unit tests) continue to pass.

- [ ] **Step 8: Commit**

```bash
git add src/Pricing/Program.cs tests/Pricing.Tests/
git commit -m "test(pricing): add failing integration tests for price history endpoints"
```

---

### Task 5: Endpoints — Make Tests Green

**Goal:** Add `POST /prices/{cardId}` and `GET /prices/{cardId}/history` endpoints to `Program.cs`. All 3 integration tests must pass.

**Files:**
- Modify: `src/Pricing/Program.cs` — add the two endpoints between the startup block and `app.Run()`

**Interfaces:**
- Consumes: `IPriceRepository.AddAsync`, `IPriceRepository.GetHistoryAsync`, `PriceSnapshot.Create`, `RecordPriceRequest`, `PriceHistoryResponse`, `PriceBucket`, `Granularity` enum — all defined in Tasks 2 and 3

- [ ] **Step 1: Write the two endpoints in Program.cs**

Replace `src/Pricing/Program.cs` with the full final version:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Pricing.Application.DTOs;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Domain.Enums;
using TCGTrading.Pricing.Infrastructure.Persistence;
using TCGTrading.Pricing.Infrastructure.Seed;
using TCGTrading.Pricing.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("pricing");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddSource("tcgtrading.pricing"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddSingleton<IPricingTelemetry, PricingTelemetry>();
builder.AddPriceHistory();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<PricingMigrationService>();
    await migrator.ApplyAsync();

    var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
    if (!await repo.HasAnyAsync())
        await PriceSeed.SeedAsync(repo);
}

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.MapPost("/prices/{cardId:guid}", async (
    Guid cardId,
    RecordPriceRequest request,
    IPriceRepository repo,
    CancellationToken ct) =>
{
    if (request.Price <= 0)
        return Results.BadRequest("Price must be greater than 0.");

    var currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency;
    var source   = string.IsNullOrWhiteSpace(request.Source)   ? "manual" : request.Source;

    var snapshot = PriceSnapshot.Create(
        cardId, request.Price, currency, source, DateTimeOffset.UtcNow);
    await repo.AddAsync(snapshot, ct);

    return Results.Created(
        $"/prices/{cardId}/history",
        new { cardId = snapshot.CardId, price = snapshot.Price, currency = snapshot.Currency, recordedAt = snapshot.RecordedAt });
});

app.MapGet("/prices/{cardId:guid}/history", async (
    Guid cardId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    string? granularity,
    IPriceRepository repo,
    CancellationToken ct) =>
{
    var gran = granularity?.ToLowerInvariant() switch
    {
        null   => Granularity.Day,
        "hour" => Granularity.Hour,
        "day"  => Granularity.Day,
        "week" => Granularity.Week,
        _      => (Granularity?)null,
    };

    if (gran is null)
        return Results.BadRequest("granularity must be 'hour', 'day', or 'week'.");

    var toTime   = to   ?? DateTimeOffset.UtcNow;
    var fromTime = from ?? toTime.AddDays(-30);

    var buckets = await repo.GetHistoryAsync(cardId, fromTime, toTime, gran.Value, ct);

    return Results.Ok(new PriceHistoryResponse(
        cardId,
        gran.Value.ToString().ToLowerInvariant(),
        fromTime,
        toTime,
        buckets));
});

app.Run();

public partial class Program { }
```

**Endpoint notes:**
- `{cardId:guid}` route constraint validates that `cardId` is a well-formed GUID before reaching handler code.
- Default granularity (null) maps to `Granularity.Day`.

- [ ] **Step 2: Build**

```bash
dotnet build src/Pricing/TCGTrading.Pricing.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Run all integration tests — expect 3 passes**

```bash
dotnet test tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj --logger "console;verbosity=normal" --filter "FullyQualifiedName~PriceHistoryIntegrationTests"
```

Expected: `3 passed, 0 failed`. The full suite (including `PricingTelemetryTests`) should also pass:

```bash
dotnet test tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj --logger "console;verbosity=normal"
```

Expected: `5 passed, 0 failed` (3 new integration tests + 2 existing telemetry unit tests).

- [ ] **Step 4: Commit**

```bash
git add src/Pricing/Program.cs
git commit -m "feat(pricing): add price history endpoints — POST /prices/{cardId}, GET /prices/{cardId}/history"
```
