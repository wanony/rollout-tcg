# Price Alerts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user set a price target on a card and get a real-time notification when the market price crosses it, driven by a real background price-ingestion pipeline.

**Architecture:** Three services, one responsibility each. **Pricing** gains a scheduled TCGdex ingestion worker that writes snapshots and publishes `CardPriceUpdatedEvent` (via transactional outbox); it also maintains a `WatchedCard` projection (event-sourced from alert lifecycle events) that scopes what it polls. A **new Alerts service** owns alert-rule CRUD, a per-card last-seen-price projection, and the edge-triggered firing policy; it consumes `CardPriceUpdatedEvent` and publishes `PriceAlertTriggeredEvent`. **Notification** (unchanged role) consumes that and does fan-out (persist + SignalR push).

**Tech Stack:** .NET 10 · ASP.NET Core Minimal APIs · EF Core + Npgsql · MassTransit + RabbitMQ (with EF outbox/inbox) · Polly · Dapper (Pricing time-series only) · YARP gateway · React/TS frontend.

## Global Constraints

- **.NET 10**, `Microsoft.NET.Sdk.Web`, nullable enabled (match existing services). Package versions are centrally pinned in `Directory.Packages.props` — add `<PackageVersion>` entries there, reference without version in csproj.
- **Currency is USD only.** Alerts pin to USD to match `CardPriceUpdatedEvent.MarketPriceUsd`. No FX.
- **CardId identity:** the string provider id (e.g. `"basep-1"`) is the cross-service card key everywhere except Pricing's TimescaleDB snapshots (which use a `Guid`). `CardPriceUpdatedEvent` already carries **both** `CardId` (Guid) and `ProviderCardId` (string). Alerts key on `ProviderCardId`.
- **Integration events** live in `src/SharedKernel/Contracts/`, derive from `IntegrationEvent(Guid EventId, DateTime OccurredAt)`, and are plain `record`s.
- **Messaging config** is read from `RabbitMq:Host` / `RabbitMq:Username` / `RabbitMq:Password` (env `RabbitMq__*`). Copy `AddMassTransit` shape from `src/Notification/Infrastructure/Messaging/MessagingExtensions.cs`.
- **Connection strings** via `ConnectionStrings__<Name>` env, `Host=postgres;Port=5432;Database=db_<svc>;Username=tcg;Password=dev`.
- **Telemetry:** every service calls `builder.AddTelemetry("<name>")` from SharedKernel and wires OTel tracing/Prometheus exactly like `src/Notification/Program.cs`.
- **Auth convention (follow existing, do not invent):** downstream services receive `userId` from the caller (query param on GET, body field on POST), and the gateway enforces the `authenticated` policy on protected routes. Alerts CRUD follows the same pattern as Portfolio. `// ponytail: userId is client-asserted, same as every other service; the correct fix is a gateway transform forwarding the verified sub claim as X-User-Id — separate cross-cutting change, out of scope here.`
- **Do not touch live deployment.** Everything is verified locally (localhost + Tailscale `http://100.77.135.111`) before any VPS deploy.

---

## File Structure

**SharedKernel (new contracts):**
- `src/SharedKernel/Contracts/PriceAlertTriggeredEvent.cs` — Alerts → Notification
- `src/SharedKernel/Contracts/PriceAlertCreatedEvent.cs` — Alerts → Pricing (watched-set)
- `src/SharedKernel/Contracts/PriceAlertDeletedEvent.cs` — Alerts → Pricing (watched-set)

**Pricing (ingestion + emit + watched-set):**
- `src/Pricing/Infrastructure/Messaging/MessagingExtensions.cs` — MassTransit + EF outbox + WatchedCard consumers
- `src/Pricing/Application/Interfaces/ITcgdexPriceClient.cs` — upstream price fetch
- `src/Pricing/Infrastructure/External/TcgdexPriceClient.cs` — Polly-wrapped HttpClient
- `src/Pricing/Domain/Entities/WatchedCard.cs`
- `src/Pricing/Infrastructure/Persistence/PricingOutboxDbContext.cs` — EF context for outbox + WatchedCard (relational, separate from Dapper time-series)
- `src/Pricing/Infrastructure/Messaging/PriceAlertCreatedConsumer.cs`, `PriceAlertDeletedConsumer.cs`
- `src/Pricing/Infrastructure/Ingestion/PriceIngestionWorker.cs` — `BackgroundService`, advisory-lock leader
- `src/Pricing/Application/Interfaces/IWatchedCardRepository.cs` + impl

**Alerts service (NEW):**
- `src/Alerts/TCGTrading.Alerts.Api.csproj`, `Dockerfile`, `Program.cs`
- `src/Alerts/Domain/Entities/PriceAlert.cs`, `Domain/Enums/AlertDirection.cs`, `Domain/Entities/AlertTrigger.cs`
- `src/Alerts/Domain/CrossingPolicy.cs` — **pure firing logic (the tested core)**
- `src/Alerts/Domain/Entities/LastSeenPrice.cs`
- `src/Alerts/Application/DTOs/*.cs`, `Application/Interfaces/IAlertRepository.cs`
- `src/Alerts/Infrastructure/Persistence/AlertsDbContext.cs`, `AlertRepository.cs`
- `src/Alerts/Infrastructure/Messaging/MessagingExtensions.cs`, `CardPriceUpdatedConsumer.cs`

**Notification (fan-out only):**
- `src/Notification/Infrastructure/Messaging/PriceAlertTriggeredConsumer.cs`
- `src/Notification/Domain/Enums/NotificationType.cs` — add `PriceAlert`

**Gateway / infra / frontend:**
- `src/ApiGateway/appsettings.json` — `alerts` route + cluster
- `docker-compose.yml` — `alerts` service block
- `tests/Alerts.Tests/` — CrossingPolicy unit tests + consumer harness test
- `src/Frontend/src/…` — "Set price alert" control on card detail + alerts list

---

## Phase 0 — Shared contracts

### Task 0: New integration events

**Files:**
- Create: `src/SharedKernel/Contracts/PriceAlertTriggeredEvent.cs`
- Create: `src/SharedKernel/Contracts/PriceAlertCreatedEvent.cs`
- Create: `src/SharedKernel/Contracts/PriceAlertDeletedEvent.cs`

**Interfaces produced:**
- `PriceAlertTriggeredEvent(Guid EventId, DateTime OccurredAt, Guid AlertId, Guid UserId, string ProviderCardId, string CardName, string Direction, decimal TargetPriceUsd, decimal MarketPriceUsd)`
- `PriceAlertCreatedEvent(Guid EventId, DateTime OccurredAt, Guid AlertId, string ProviderCardId)`
- `PriceAlertDeletedEvent(Guid EventId, DateTime OccurredAt, Guid AlertId, string ProviderCardId)`

- [ ] **Step 1: Write the three records**

```csharp
// PriceAlertTriggeredEvent.cs
namespace TCGTrading.SharedKernel.Contracts;

public record PriceAlertTriggeredEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AlertId,
    Guid UserId,
    string ProviderCardId,
    string CardName,
    string Direction,
    decimal TargetPriceUsd,
    decimal MarketPriceUsd) : IntegrationEvent(EventId, OccurredAt);
```

```csharp
// PriceAlertCreatedEvent.cs
namespace TCGTrading.SharedKernel.Contracts;

public record PriceAlertCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AlertId,
    string ProviderCardId) : IntegrationEvent(EventId, OccurredAt);
```

```csharp
// PriceAlertDeletedEvent.cs
namespace TCGTrading.SharedKernel.Contracts;

public record PriceAlertDeletedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid AlertId,
    string ProviderCardId) : IntegrationEvent(EventId, OccurredAt);
```

- [ ] **Step 2: Build**

Run: `dotnet build src/SharedKernel/TCGTrading.SharedKernel.csproj`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/SharedKernel/Contracts/PriceAlert*.cs
git commit -m "feat(contracts): add price-alert integration events"
```

---

## Phase 1 — Alerts service: the tested core first

Build the pure firing policy before any infrastructure. This is the review-showcase and the highest-risk logic.

### Task 1: `CrossingPolicy` — edge-triggered firing with hysteresis + cooldown

**Files:**
- Create: `src/Alerts/Domain/Enums/AlertDirection.cs`
- Create: `src/Alerts/Domain/CrossingPolicy.cs`
- Create: `src/Alerts/TCGTrading.Alerts.Api.csproj` (minimal, to compile domain)
- Test: `tests/Alerts.Tests/CrossingPolicyTests.cs`

**Interfaces produced:**
- `enum AlertDirection { Below, Above }`
- `static class CrossingPolicy { static bool ShouldFire(AlertDirection direction, decimal target, decimal? lastPrice, decimal newPrice, decimal hysteresisFraction) }`
- Semantics: `Below` fires when price transitions from **above** target to **at-or-below** `target` (a genuine downward crossing). `Above` mirrors it. First observation (`lastPrice == null`) fires if already on the trigger side — a newly-set "below $10" alert on a card already at $8 should fire. Hysteresis: for a re-arm to count, the price must first leave the trigger zone by `target * hysteresisFraction` (prevents flapping when price oscillates on the boundary) — modeled here as: a `Below` crossing only counts if `lastPrice > target * (1 + hysteresisFraction)`.

- [ ] **Step 1: Minimal csproj so the test project can reference the domain**

```xml
<!-- src/Alerts/TCGTrading.Alerts.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../SharedKernel/TCGTrading.SharedKernel.csproj" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="MassTransit" />
    <PackageReference Include="MassTransit.RabbitMQ" />
    <PackageReference Include="MassTransit.EntityFrameworkCore" />
  </ItemGroup>
</Project>
```

Add `MassTransit.EntityFrameworkCore` to `Directory.Packages.props` if absent (same version as `MassTransit`). Add project to `TCGTrading.sln`: `dotnet sln add src/Alerts/TCGTrading.Alerts.Api.csproj`.

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/Alerts.Tests/CrossingPolicyTests.cs
using TCGTrading.Alerts.Domain;
using TCGTrading.Alerts.Domain.Enums;
using Xunit;

public class CrossingPolicyTests
{
    const decimal Hyst = 0.0m; // disable hysteresis for the basic cases

    [Fact] public void Below_fires_on_downward_crossing()
        => Assert.True(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 12m, newPrice: 9m, Hyst));

    [Fact] public void Below_does_not_fire_when_already_below_and_staying_below()
        => Assert.False(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 9m, newPrice: 8m, Hyst));

    [Fact] public void Below_fires_on_first_observation_when_already_below()
        => Assert.True(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: null, newPrice: 8m, Hyst));

    [Fact] public void Below_does_not_fire_on_first_observation_when_above()
        => Assert.False(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: null, newPrice: 12m, Hyst));

    [Fact] public void Above_fires_on_upward_crossing()
        => Assert.True(CrossingPolicy.ShouldFire(AlertDirection.Above, 10m, lastPrice: 8m, newPrice: 11m, Hyst));

    [Fact] public void Hysteresis_suppresses_flapping_near_boundary()
    {
        // target 10, 5% band → downward crossing only counts if last price was > 10.5
        Assert.False(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 10.2m, newPrice: 9.9m, 0.05m));
        Assert.True (CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 11m,   newPrice: 9.9m, 0.05m));
    }
}
```

- [ ] **Step 3: Run — verify FAIL**

Run: `dotnet test tests/Alerts.Tests/Alerts.Tests.csproj`
Expected: FAIL — `CrossingPolicy` / `AlertDirection` not defined. (Create `tests/Alerts.Tests/Alerts.Tests.csproj` as an xUnit project referencing the Alerts csproj; mirror an existing `tests/*.Tests.csproj`.)

- [ ] **Step 4: Implement**

```csharp
// src/Alerts/Domain/Enums/AlertDirection.cs
namespace TCGTrading.Alerts.Domain.Enums;
public enum AlertDirection { Below, Above }
```

```csharp
// src/Alerts/Domain/CrossingPolicy.cs
using TCGTrading.Alerts.Domain.Enums;

namespace TCGTrading.Alerts.Domain;

public static class CrossingPolicy
{
    // Fires on a genuine edge crossing into the trigger zone.
    // hysteresisFraction requires the previous price to have been clear of the
    // boundary by target*fraction, so oscillation on the line doesn't re-fire.
    public static bool ShouldFire(
        AlertDirection direction, decimal target, decimal? lastPrice,
        decimal newPrice, decimal hysteresisFraction)
    {
        var band = target * hysteresisFraction;
        return direction switch
        {
            AlertDirection.Below =>
                newPrice <= target &&
                (lastPrice is null ? true : lastPrice > target + band),
            AlertDirection.Above =>
                newPrice >= target &&
                (lastPrice is null ? true : lastPrice < target - band),
            _ => false,
        };
    }
}
```

- [ ] **Step 5: Run — verify PASS**

Run: `dotnet test tests/Alerts.Tests/Alerts.Tests.csproj`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Alerts tests/Alerts.Tests Directory.Packages.props TCGTrading.sln
git commit -m "feat(alerts): edge-triggered CrossingPolicy with hysteresis (TDD)"
```

### Task 2: Alerts domain entities

**Files:**
- Create: `src/Alerts/Domain/Entities/PriceAlert.cs`
- Create: `src/Alerts/Domain/Entities/AlertTrigger.cs`
- Create: `src/Alerts/Domain/Entities/LastSeenPrice.cs`

**Interfaces produced:**
- `PriceAlert` fields: `Guid Id`, `Guid UserId`, `string ProviderCardId`, `string CardName`, `AlertDirection Direction`, `decimal TargetPriceUsd`, `bool Recurring`, `bool IsActive`, `DateTime? LastFiredAt`, `DateTime CreatedAt`. Methods: `static Create(userId, providerCardId, cardName, direction, target, recurring)`; `void Fire(DateTime at)` → sets `LastFiredAt`, and if `!Recurring` sets `IsActive=false`.
- `AlertTrigger` fields: `Guid Id`, `Guid AlertId`, `decimal MarketPriceUsd`, `DateTime FiredAt`. Used for idempotency + history.
- `LastSeenPrice` fields: `string ProviderCardId` (PK), `decimal MarketPriceUsd`, `DateTime UpdatedAt`.

- [ ] **Step 1:** Write the three entities following the `CollectionItem` shape (private setters, static `Create`, `Entity` base from SharedKernel where an `Id`/`CreatedAt` is wanted; `LastSeenPrice` is a plain keyed entity).

```csharp
// src/Alerts/Domain/Entities/PriceAlert.cs
using TCGTrading.Alerts.Domain.Enums;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.Alerts.Domain.Entities;

public class PriceAlert : Entity
{
    public Guid UserId { get; private init; }
    public string ProviderCardId { get; private init; } = string.Empty;
    public string CardName { get; private init; } = string.Empty;
    public AlertDirection Direction { get; private init; }
    public decimal TargetPriceUsd { get; private init; }
    public bool Recurring { get; private init; }
    public bool IsActive { get; private set; }
    public DateTime? LastFiredAt { get; private set; }

    private PriceAlert() { }

    public static PriceAlert Create(Guid userId, string providerCardId, string cardName,
        AlertDirection direction, decimal targetPriceUsd, bool recurring)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetPriceUsd);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCardId);
        return new PriceAlert
        {
            UserId = userId, ProviderCardId = providerCardId, CardName = cardName,
            Direction = direction, TargetPriceUsd = targetPriceUsd,
            Recurring = recurring, IsActive = true,
        };
    }

    public void Fire(DateTime at)
    {
        LastFiredAt = at;
        if (!Recurring) IsActive = false;
    }
}
```

`AlertTrigger` and `LastSeenPrice` analogous (write them out fully — no "similar to").

- [ ] **Step 2:** Build `dotnet build src/Alerts/TCGTrading.Alerts.Api.csproj` → PASS.
- [ ] **Step 3:** Commit `feat(alerts): domain entities (PriceAlert, AlertTrigger, LastSeenPrice)`.

### Task 3: Persistence — AlertsDbContext + repository + MassTransit outbox tables

**Files:**
- Create: `src/Alerts/Infrastructure/Persistence/AlertsDbContext.cs`
- Create: `src/Alerts/Application/Interfaces/IAlertRepository.cs`
- Create: `src/Alerts/Infrastructure/Persistence/AlertRepository.cs`
- Create: `src/Alerts/Infrastructure/Persistence/PersistenceExtensions.cs`
- Create: migration via `dotnet ef migrations add InitialCreate`

**Interfaces produced (`IAlertRepository`):**
```csharp
Task AddAsync(PriceAlert alert, CancellationToken ct = default);
Task<IReadOnlyList<PriceAlert>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
Task<PriceAlert?> GetByIdAsync(Guid id, CancellationToken ct = default);
Task<IReadOnlyList<PriceAlert>> GetActiveByCardAsync(string providerCardId, CancellationToken ct = default);
Task<LastSeenPrice?> GetLastSeenAsync(string providerCardId, CancellationToken ct = default);
Task<bool> TriggerExistsAsync(Guid alertId, decimal marketPriceUsd, CancellationToken ct = default);
Task SaveChangesAsync(CancellationToken ct = default); // consumer commits alert.Fire + LastSeen + trigger atomically
Task DeleteAsync(PriceAlert alert, CancellationToken ct = default);
Task UpsertLastSeenAsync(string providerCardId, decimal price, DateTime at, CancellationToken ct = default);
Task AddTriggerAsync(AlertTrigger trigger, CancellationToken ct = default);
```

- [ ] **Step 1:** DbContext with `DbSet<PriceAlert>`, `DbSet<AlertTrigger>`, `DbSet<LastSeenPrice>`. In `OnModelCreating`: `LastSeenPrice` keyed on `ProviderCardId`; unique index on `AlertTrigger(AlertId, MarketPriceUsd)` for idempotency; **add the MassTransit outbox+inbox EF entities**: `modelBuilder.AddInboxStateEntity(); modelBuilder.AddOutboxMessageEntity(); modelBuilder.AddOutboxStateEntity();`.

```csharp
// PersistenceExtensions.cs — mirror Notification's, plus the repo registration
public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
{
    builder.Services.AddDbContext<AlertsDbContext>(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString("AlertsDb")));
    builder.Services.AddScoped<IAlertRepository, AlertRepository>();
    return builder;
}
```

- [ ] **Step 2:** Implement `AlertRepository` over `AlertsDbContext` (mirror `NotificationRepository`; `GetActiveByCardAsync` → `Where(a => a.ProviderCardId == id && a.IsActive)`; `TriggerExistsAsync` → `AlertTriggers.AnyAsync(...)`).
- [ ] **Step 3:** `dotnet ef migrations add InitialCreate -p src/Alerts/TCGTrading.Alerts.Api.csproj` (add a `DbContextFactory` like Notification's so the design-time tool works). Build → PASS.
- [ ] **Step 4:** Commit `feat(alerts): AlertsDbContext, repository, EF outbox tables + migration`.

### Task 4: Messaging — consume `CardPriceUpdatedEvent`, evaluate, publish `PriceAlertTriggeredEvent`

**Files:**
- Create: `src/Alerts/Infrastructure/Messaging/MessagingExtensions.cs`
- Create: `src/Alerts/Infrastructure/Messaging/CardPriceUpdatedConsumer.cs`
- Test: `tests/Alerts.Tests/CardPriceUpdatedConsumerTests.cs`

**Interfaces consumed:** `CrossingPolicy.ShouldFire`, `IAlertRepository`, `IPublishEndpoint`.
**Interfaces produced:** `CardPriceUpdatedConsumer : IConsumer<CardPriceUpdatedEvent>`.

- [ ] **Step 1: Write the failing consumer test (MassTransit InMemoryTestHarness)**

```csharp
// tests/Alerts.Tests/CardPriceUpdatedConsumerTests.cs
[Fact]
public async Task Publishes_triggered_event_when_alert_crosses()
{
    // Arrange: in-memory AlertsDbContext with one active Below@10 alert on "basep-1",
    // LastSeen = 12. Harness with CardPriceUpdatedConsumer.
    // Act: publish CardPriceUpdatedEvent(ProviderCardId:"basep-1", MarketPriceUsd:9).
    // Assert: harness Published<PriceAlertTriggeredEvent> == true; alert.IsActive == false (one-shot).
}

[Fact]
public async Task Does_not_publish_when_price_does_not_cross() { /* newPrice 11, no fire */ }

[Fact]
public async Task Is_idempotent_on_redelivery() { /* same event twice → one trigger row, one publish */ }
```

Use `Microsoft.EntityFrameworkCore.InMemory` (add to test csproj) or a disposable Postgres; in-memory is fine here since the logic under test is the policy + dedup, not SQL.

- [ ] **Step 2:** Run → FAIL (consumer not defined).

- [ ] **Step 3: Implement the consumer**

```csharp
// CardPriceUpdatedConsumer.cs
using MassTransit;
using TCGTrading.Alerts.Application.Interfaces;
using TCGTrading.Alerts.Domain;
using TCGTrading.Alerts.Domain.Entities;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Alerts.Infrastructure.Messaging;

public class CardPriceUpdatedConsumer(IAlertRepository repo) : IConsumer<CardPriceUpdatedEvent>
{
    const decimal Hysteresis = 0.02m; // 2% band

    public async Task Consume(ConsumeContext<CardPriceUpdatedEvent> ctx)
    {
        var e = ctx.Message;
        var last = await repo.GetLastSeenAsync(e.ProviderCardId, ctx.CancellationToken);
        var alerts = await repo.GetActiveByCardAsync(e.ProviderCardId, ctx.CancellationToken);

        foreach (var alert in alerts)
        {
            if (!CrossingPolicy.ShouldFire(alert.Direction, alert.TargetPriceUsd,
                    last?.MarketPriceUsd, e.MarketPriceUsd, Hysteresis))
                continue;

            // idempotency: same (alert, price) already recorded → skip
            if (await repo.TriggerExistsAsync(alert.Id, e.MarketPriceUsd, ctx.CancellationToken))
                continue;

            alert.Fire(e.OccurredAt);
            await repo.AddTriggerAsync(new AlertTrigger(alert.Id, e.MarketPriceUsd, e.OccurredAt), ctx.CancellationToken);
            await ctx.Publish(new PriceAlertTriggeredEvent(
                Guid.NewGuid(), DateTime.UtcNow, alert.Id, alert.UserId,
                alert.ProviderCardId, alert.CardName, alert.Direction.ToString(),
                alert.TargetPriceUsd, e.MarketPriceUsd));
        }

        await repo.UpsertLastSeenAsync(e.ProviderCardId, e.MarketPriceUsd, e.OccurredAt, ctx.CancellationToken);
        await repo.SaveChangesAsync(ctx.CancellationToken);
    }
}
```

> The `ctx.Publish` inside a consumer wired with the MassTransit EF outbox is captured in the same DB transaction as `SaveChangesAsync` — atomic evaluate-persist-publish. `MessagingExtensions` must enable the outbox: `x.AddEntityFrameworkOutbox<AlertsDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); });` and `x.AddConsumer<CardPriceUpdatedConsumer>();`.

- [ ] **Step 4:** Run tests → PASS (3 tests).
- [ ] **Step 5:** Commit `feat(alerts): CardPriceUpdated consumer, edge eval + idempotent publish via outbox`.

### Task 5: Alerts API + Program.cs + Dockerfile

**Files:**
- Create: `src/Alerts/Application/DTOs/CreateAlertRequest.cs`, `AlertResponse.cs`
- Create: `src/Alerts/Program.cs`
- Create: `src/Alerts/Dockerfile` (copy `src/Notification/Dockerfile`, swap paths/port to 5007)

**Interfaces produced (REST, via gateway `/api/alerts`):**
- `POST /alerts` body `CreateAlertRequest(Guid UserId, string ProviderCardId, string CardName, string Direction, decimal TargetPriceUsd, bool Recurring)` → 201 `AlertResponse`. On create, **publish `PriceAlertCreatedEvent`** (outbox) so Pricing starts watching the card.
- `GET /alerts?userId={guid}` → `AlertResponse[]`
- `DELETE /alerts/{id:guid}` → 204, **publish `PriceAlertDeletedEvent`**.
- `GET /health`.

- [ ] **Step 1:** DTOs + `Program.cs` mirroring `Notification/Program.cs` (telemetry, `AddPersistence`, `AddMessaging`, migrate on startup, Prometheus, health). Parse `Direction` string → `AlertDirection` (400 on bad value). Endpoints call repo + `IPublishEndpoint`.
- [ ] **Step 2:** Add `public partial class Program { }` at end (test harness needs it).
- [ ] **Step 3:** Build → PASS. Manual smoke deferred to Task 9.
- [ ] **Step 4:** Commit `feat(alerts): CRUD API, Program, Dockerfile`.

---

## Phase 2 — Pricing: real ingestion + emit + watched-set

### Task 6: Pricing publishes `CardPriceUpdatedEvent` + MassTransit/outbox + WatchedCard projection

**Files:**
- Create: `src/Pricing/Infrastructure/Persistence/PricingOutboxDbContext.cs` (EF, relational — holds `WatchedCard` + MassTransit outbox/inbox; **separate** from the Dapper time-series path)
- Create: `src/Pricing/Domain/Entities/WatchedCard.cs` (`string ProviderCardId` PK, `Guid CardId`, `int RefCount`, `DateTime UpdatedAt`)
- Create: `src/Pricing/Application/Interfaces/IWatchedCardRepository.cs` + `Infrastructure/Persistence/WatchedCardRepository.cs`
- Create: `src/Pricing/Infrastructure/Messaging/MessagingExtensions.cs`, `PriceAlertCreatedConsumer.cs`, `PriceAlertDeletedConsumer.cs`
- Modify: `src/Pricing/TCGTrading.Pricing.Api.csproj` (add EF Core, Npgsql.EntityFrameworkCore.PostgreSQL, MassTransit, MassTransit.RabbitMQ, MassTransit.EntityFrameworkCore)
- Modify: `src/Pricing/Program.cs`

**Interfaces produced:**
- `IWatchedCardRepository { Task<IReadOnlyList<WatchedCard>> GetAllAsync(ct); Task IncrementAsync(string providerCardId, ct); Task DecrementAsync(string providerCardId, ct); }`
- `PriceAlertCreatedConsumer`/`PriceAlertDeletedConsumer` maintain refcount (increment on create, decrement on delete; delete row at 0).

- [ ] **Step 1:** Add `WatchedCard` entity + EF context + migration (`AlertsDb`-style). RefCount so N alerts on one card = one watched row.
- [ ] **Step 2:** `MessagingExtensions.AddMessaging` — RabbitMq host config (copy Notification), `x.AddConsumer<PriceAlertCreatedConsumer>(); x.AddConsumer<PriceAlertDeletedConsumer>();`, `x.AddEntityFrameworkOutbox<PricingOutboxDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); });`, `cfg.ConfigureEndpoints(ctx)`.
- [ ] **Step 3:** In `Program.cs`, add `builder.AddMessaging()` + register `PricingOutboxDbContext`/repo, migrate on startup. Extend `POST /prices/{cardId:guid}` to also accept `ProviderCardId` on `RecordPriceRequest` and, after `repo.AddAsync`, `await publish.Publish(new CardPriceUpdatedEvent(Guid.NewGuid(), DateTime.UtcNow, cardId, request.ProviderCardId, "tcgdex", request.Price, request.Price, request.Price))`. `// ponytail: low/high == market for now; populate real low/high when TCGdex client lands (Task 7).`
- [ ] **Step 4:** Add `WatchedCardTests` (refcount increment/decrement to zero removes row). Run → PASS.
- [ ] **Step 5:** Commit `feat(pricing): emit CardPriceUpdatedEvent via outbox + WatchedCard projection`.

### Task 7: TCGdex price client (Polly)

**Files:**
- Create: `src/Pricing/Application/Interfaces/ITcgdexPriceClient.cs`
- Create: `src/Pricing/Infrastructure/External/TcgdexPriceClient.cs`
- Modify: csproj (`Microsoft.Extensions.Http.Polly` / `Microsoft.Extensions.Http.Resilience`)
- Test: `tests/Pricing.Tests/TcgdexPriceClientTests.cs` (mock `HttpMessageHandler`)

**Interfaces produced:** `ITcgdexPriceClient { Task<decimal?> GetMarketPriceUsdAsync(string providerCardId, CancellationToken ct) }`. Fetches `https://api.tcgdex.net/v2/en/cards/{id}`, reads `pricing.tcgplayer.<variant>.marketPrice` (USD) — falls back across variants (holofoil/normal/reverse). Returns null if no price. `// ponytail: field path mirrors what CardDetailModal.tsx already parses; verify against a live TCGdex response before shipping.`

- [ ] **Step 1:** Write failing test: given a canned TCGdex JSON body, client returns the expected market price; given a body with no pricing, returns null.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement typed `HttpClient` registered with `AddHttpClient<ITcgdexPriceClient, TcgdexPriceClient>().AddStandardResilienceHandler()` (retry+backoff+circuit-breaker). Parse with `System.Text.Json`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(pricing): Polly-wrapped TCGdex price client`.

### Task 8: Ingestion worker (leader-elected BackgroundService)

**Files:**
- Create: `src/Pricing/Infrastructure/Ingestion/PriceIngestionWorker.cs`
- Modify: `src/Pricing/Program.cs` (`builder.Services.AddHostedService<PriceIngestionWorker>();`)
- Test: `tests/Pricing.Tests/PriceIngestionWorkerTests.cs` (one loop iteration writes a snapshot + publishes per watched card)

**Behavior:** every `PollIntervalSeconds` (config, default 300): acquire a Postgres advisory lock (`pg_try_advisory_lock`); if not acquired, skip (another replica owns this tick). For each `WatchedCard`: `GetMarketPriceUsdAsync`; if changed vs latest snapshot, write snapshot (Dapper repo) + publish `CardPriceUpdatedEvent`. Release lock.

- [ ] **Step 1:** Failing test: worker with a stub `ITcgdexPriceClient` returning 9.00 and one watched card → one snapshot recorded, one `CardPriceUpdatedEvent` published. (Drive one iteration directly, not the timer.)
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement. `// ponytail: single-replica advisory-lock scheduler; swap for Quartz clustered scheduling if Pricing ever needs richer/replicated scheduling.`
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(pricing): scheduled ingestion worker with advisory-lock leader`.

---

## Phase 3 — Notification fan-out

### Task 9: Notification consumes `PriceAlertTriggeredEvent`

**Files:**
- Create: `src/Notification/Infrastructure/Messaging/PriceAlertTriggeredConsumer.cs`
- Modify: `src/Notification/Domain/Enums/NotificationType.cs` → `enum NotificationType { ListingPurchased, ListingSold, PriceAlert }`
- Modify: `src/Notification/Infrastructure/Messaging/MessagingExtensions.cs` → `x.AddConsumer<PriceAlertTriggeredConsumer>();`
- Test: `tests/Notification.Tests/PriceAlertTriggeredConsumerTests.cs`

- [ ] **Step 1:** Failing test: harness publishes `PriceAlertTriggeredEvent` → a `UserNotification` is persisted for `UserId` with `Type.PriceAlert` and pushed via hub (assert repo.Add called / SignalR mock).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement mirroring `ListingPurchasedConsumer`:

```csharp
var notif = UserNotification.Create(
    e.UserId,
    title: "Price alert",
    message: $"{e.CardName} is now ${e.MarketPriceUsd:F2} ({e.Direction.ToLower()} your ${e.TargetPriceUsd:F2} target).",
    NotificationType.PriceAlert,
    referenceId: e.AlertId);
await repo.AddAsync(notif, ctx.CancellationToken);
await PushAsync(e.UserId, notif);
```

- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(notification): fan out PriceAlertTriggered to user + SignalR`.

---

## Phase 4 — Gateway, compose, frontend, end-to-end

### Task 10: Gateway route + docker-compose service

**Files:**
- Modify: `src/ApiGateway/appsettings.json` (+ `appsettings.Development.json` cluster if it mirrors)
- Modify: `docker-compose.yml`

- [ ] **Step 1:** Add route + cluster (auth like portfolio):

```json
"alerts-route": {
  "ClusterId": "alerts",
  "AuthorizationPolicy": "authenticated",
  "Match": { "Path": "/api/alerts/{**rest}" },
  "Transforms": [{ "PathPattern": "/alerts/{**rest}" }]
}
```
```json
"alerts": { "Destinations": { "primary": { "Address": "http://alerts:5007" } } }
```

- [ ] **Step 2:** Add `alerts` service to `docker-compose.yml` (copy the `notification` block; port `5007`, `ConnectionStrings__AlertsDb`, `RabbitMq__*`, depends_on postgres+rabbitmq healthy, health `curl -sf http://localhost:5007/health`). Add `Pricing` the `RabbitMq__*` env + `ConnectionStrings__PricingOutboxDb` and a TCGdex base URL if configurable. Ensure `db_alerts` / `db_pricing_outbox` get created (init script or `EnsureCreated`/migrate on startup — migrate is already the pattern).
- [ ] **Step 3:** Commit `feat(infra): gateway route + compose service for alerts; pricing messaging env`.

### Task 11: Frontend — set alert + list alerts

**Files:**
- Modify: `src/Frontend/src/components/CardDetailModal.tsx` (add "Set price alert" control under the price/sparkline)
- Create: `src/Frontend/src/api/alerts.ts` (POST/GET/DELETE `/api/alerts`)
- Create/modify: an alerts list view (reuse notification bell; a fired alert already arrives as a normal notification via SignalR)
- Modify: `src/Frontend/src/types/api.ts` (`AlertResponse`, `CreateAlertRequest`)

- [ ] **Step 1:** Add types + `alerts.ts` using the existing authenticated fetch wrapper (same one portfolio uses — reuse, do not add a client).
- [ ] **Step 2:** In `CardDetailModal`, add a direction toggle (Below/Above) + price input + "Recurring" checkbox + "Create alert" button calling `POST /api/alerts` with the card's provider id and name. Show existing alerts for the card with a delete button.
- [ ] **Step 3:** Manual verify in Task 12 (no unit test unless a pure helper is extracted).
- [ ] **Step 4:** Commit `feat(frontend): set/list price alerts on card detail`.

### Task 12: End-to-end verification (local only)

- [ ] **Step 1:** `dotnet build TCGTrading.sln` → PASS. `dotnet test` → all green.
- [ ] **Step 2:** `docker compose up -d --build`; `docker compose ps` → all healthy (now 14 services incl. `alerts`).
- [ ] **Step 3:** Log in as `demo@rollout.dev`. Open a card (e.g. `basep-1` Pikachu). Create a "Below $9999" alert (guaranteed to be already-below → fires on first ingestion).
- [ ] **Step 4:** Verify: within one poll interval a notification appears (SignalR toast + bell). Check `docker compose logs alerts pricing notification` for the event chain: `CardPriceUpdatedEvent` → `PriceAlertTriggeredEvent` → notification persisted.
- [ ] **Step 5:** Verify idempotency: no duplicate notification on repeated identical ingestion. Verify one-shot: non-recurring alert flips `IsActive=false` (GET `/api/alerts`).
- [ ] **Step 6:** Hand back **both** verify URLs: `http://localhost` and `http://100.77.135.111` (Tailscale). **Do not deploy to the VPS** until confirmed.

---

## Self-Review notes

- **Spec coverage:** ingestion (T6-8), self-describing/pure event + consumer-owned last-seen (T3-4), separate evaluate vs fan-out (T4/T9), outbox+idempotency (T3-4,6), edge+hysteresis+cooldown (T1, recurring flag T2), watched-set via events (T6), no new deps beyond Polly/MassTransit.EF (justified), USD-only + no rules-DSL (honored). ✅
- **Type consistency:** `ProviderCardId` (string) is the cross-service key throughout; `CrossingPolicy.ShouldFire` signature identical in T1 test, T4 consumer; `PriceAlertTriggeredEvent` field list identical in T0/T4/T9.
- **Deferred (ponytail):** advisory-lock scheduler → Quartz if replicated; client-asserted userId → gateway sub-forwarding; TCGdex field path → verify live; low/high price == market until client lands.
