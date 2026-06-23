# Full-Text Search — Design Spec

**Date:** 2026-06-23
**Status:** Approved
**Scope:** Card catalog service gets a full domain model, EF Core persistence, and MeiliSearch full-text search. Exposes a fuzzy search endpoint and a card creation endpoint.

---

## Goal

Replace the CardCatalog stub with a real service: cards stored in PostgreSQL, indexed in MeiliSearch, and searchable via a REST endpoint. Demonstrates clean architecture, search infrastructure integration, and testable design — hirability signals for the portfolio.

---

## Architecture

```
Client → GET /cards/search → CardCatalog
                                  │
                    ┌─────────────┴──────────────┐
                    │                            │
              PostgreSQL                    MeiliSearch
             (source of                   (search index
               truth)                     — read path)
```

All search traffic goes to MeiliSearch. PostgreSQL is the source of truth — it feeds the index on write and re-syncs it at startup.

---

## Domain Model

**`Card` entity** — simple, TCG-agnostic:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Card name |
| `Set` | `string` | Set/expansion name |
| `Rarity` | `Rarity` enum | Common/Uncommon/Rare/UltraRare/SecretRare |
| `Type` | `string` | E.g. "Creature", "Spell", "Trap" |
| `Text` | `string` | Card effect text |

**`Rarity` enum:**
```
Common = 0
Uncommon = 1
Rare = 2
UltraRare = 3
SecretRare = 4
```

---

## Files

### New

```
src/CardCatalog/Domain/Entities/Card.cs
src/CardCatalog/Domain/Enums/Rarity.cs
src/CardCatalog/Application/Interfaces/ICardRepository.cs
src/CardCatalog/Application/Interfaces/ICardSearchService.cs
src/CardCatalog/Application/DTOs/CardSearchResult.cs
src/CardCatalog/Application/DTOs/CardSearchResponse.cs
src/CardCatalog/Infrastructure/Persistence/CardCatalogDbContext.cs
src/CardCatalog/Infrastructure/Persistence/CardRepository.cs
src/CardCatalog/Infrastructure/Persistence/PersistenceExtensions.cs
src/CardCatalog/Infrastructure/Search/MeiliSearchCardSearchService.cs
src/CardCatalog/Infrastructure/Search/SearchExtensions.cs
src/CardCatalog/Infrastructure/Seed/CardSeed.cs
tests/CardCatalog.Tests/TCGTrading.CardCatalog.Api.Tests.csproj
tests/CardCatalog.Tests/test.runsettings
tests/CardCatalog.Tests/GlobalUsings.cs
tests/CardCatalog.Tests/Fixtures/CardCatalogFixture.cs
tests/CardCatalog.Tests/Integration/SearchIntegrationTests.cs
```

### Modified

```
src/CardCatalog/TCGTrading.CardCatalog.Api.csproj      — add EF Core, Meilisearch, Npgsql packages
src/CardCatalog/Program.cs                              — wire persistence, search, seed, endpoints
src/CardCatalog/appsettings.json                        — add ConnectionStrings + MeiliSearch config
src/CardCatalog/appsettings.Development.json            — local connection strings
src/CardCatalog/Dockerfile                              — no change needed (SharedKernel already copied)
docker-compose.yml                                      — add meilisearch service
Directory.Packages.props                                — add Meilisearch, Testcontainers.MeiliSearch
```

---

## Application Layer Interfaces

### `ICardRepository`

```csharp
namespace TCGTrading.CardCatalog.Application.Interfaces;

public interface ICardRepository
{
    Task<Card> AddAsync(Card card, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> GetAllAsync(CancellationToken ct = default);
}
```

### `ICardSearchService`

```csharp
namespace TCGTrading.CardCatalog.Application.Interfaces;

public interface ICardSearchService
{
    Task IndexAsync(Card card, CancellationToken ct = default);
    Task IndexAllAsync(IReadOnlyList<Card> cards, CancellationToken ct = default);
    Task<CardSearchResponse> SearchAsync(
        string? query,
        string? set,
        string? rarity,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
```

### `CardSearchResult` and `CardSearchResponse` DTOs

```csharp
namespace TCGTrading.CardCatalog.Application.DTOs;

public sealed record CardSearchResult(
    Guid Id,
    string Name,
    string Set,
    string Rarity,
    string Type,
    string Text);

public sealed record CardSearchResponse(
    IReadOnlyList<CardSearchResult> Items,
    long Total,
    int Page,
    int PageSize);
```

---

## Infrastructure

### MeiliSearch Index Configuration

Index name: `cards`

Searchable attributes (order matters — higher = more weight):
1. `name`
2. `type`
3. `set`
4. `text`

Filterable attributes: `set`, `rarity`, `type`

Sortable attributes: `name`

### `MeiliSearchCardSearchService`

Wraps the `MeilisearchClient` from the `Meilisearch` NuGet package. Uses the `IndexAsync`, `AddDocumentsAsync`, and `SearchAsync` APIs.

Search builds a MeiliSearch filter string from the optional `set` and `rarity` query parameters:
- `set` filter: `set = "Core Set 2024"` (exact match)
- `rarity` filter: `rarity = "Rare"` (stored as string name in index, not enum int)

MeiliSearch documents store `rarity` as the enum member name string (e.g., `"SecretRare"`), not the integer value. EF Core stores it as an integer in Postgres.

Pagination: `offset = (page - 1) * pageSize`, `limit = pageSize`. Page is 1-based; offset is 0-based.

### `SearchExtensions`

```csharp
public static IHostApplicationBuilder AddSearch(this IHostApplicationBuilder builder)
```

Registers:
- `MeilisearchClient` as singleton using `MeiliSearch:Host` + `MeiliSearch:ApiKey` config keys
- `ICardSearchService` → `MeiliSearchCardSearchService`

### `PersistenceExtensions`

```csharp
public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
```

Registers:
- `CardCatalogDbContext` using `ConnectionStrings:CardDb` config key
- `ICardRepository` → `CardRepository`

---

## API Endpoints

### `GET /cards/search`

Query parameters:
- `q` — free-text search query (optional; empty or absent = return all)
- `set` — filter by set name (optional, exact match)
- `rarity` — filter by rarity string (optional, exact match: `Common`, `Uncommon`, `Rare`, `UltraRare`, `SecretRare`)
- `page` — 1-based page number (default: 1)
- `pageSize` — items per page (default: 20, max: 100)

Response `200 OK`:
```json
{
  "items": [
    {
      "id": "...",
      "name": "Dragon Fire",
      "set": "Core Set 2024",
      "rarity": "Rare",
      "type": "Spell",
      "text": "Deal 3 damage to any target."
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

### `POST /cards`

Creates a card in Postgres and indexes it in MeiliSearch. Returns `201 Created` with the created card.

Request body:
```json
{
  "name": "Dragon Fire",
  "set": "Core Set 2024",
  "rarity": "Rare",
  "type": "Spell",
  "text": "Deal 3 damage to any target."
}
```

If MeiliSearch indexing fails, the card is still created in Postgres (log the error, do not fail the request). Search index will be repaired on next startup via `IndexAllAsync`.

---

## Startup Sequence

`Program.cs` startup:
1. `builder.AddTelemetry("card-catalog")`
2. `builder.AddPersistence()`
3. `builder.AddSearch()`
4. `builder.Services.AddOpenTelemetry()...`
5. After `app.Build()`: apply EF migrations, seed cards if table empty, index all cards in MeiliSearch
6. Register endpoints

```csharp
// Apply migrations and seed
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CardCatalogDbContext>();
await db.Database.MigrateAsync();
var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
var searchService = scope.ServiceProvider.GetRequiredService<ICardSearchService>();
var existing = await repo.GetAllAsync();
if (existing.Count == 0)
{
    foreach (var card in CardSeed.Cards)
        await repo.AddAsync(card);
    existing = await repo.GetAllAsync();
}
await searchService.IndexAllAsync(existing);
```

---

## Seed Data

5 hardcoded cards covering distinct names, sets, rarities, and types:

| Name | Set | Rarity | Type | Text |
|------|-----|--------|------|------|
| Dragon Fire | Core Set 2024 | Rare | Spell | Deal 3 damage to any target. |
| Shadow Knight | Core Set 2024 | Uncommon | Creature | First strike. When ~ enters, scry 1. |
| Forest Guardian | Wilds of Nature | Common | Creature | Reach. Whenever ~ blocks, gain 2 life. |
| Lightning Bolt | Starter Pack | Common | Spell | Deal 1 damage to any target. Fast. |
| Ancient Dragon | Wilds of Nature | SecretRare | Creature | Flying, trample. ETB: deal 5 to each opponent. |

---

## Configuration

`appsettings.json`:
```json
{
  "ConnectionStrings": {
    "CardDb": "Host=postgres;Port=5432;Database=db_cards;Username=tcg;Password=dev"
  },
  "MeiliSearch": {
    "Host": "http://meilisearch:7700",
    "ApiKey": "masterKey"
  }
}
```

`appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "CardDb": "Host=localhost;Port=5432;Database=db_cards;Username=tcg;Password=dev"
  },
  "MeiliSearch": {
    "Host": "http://localhost:7700",
    "ApiKey": "masterKey"
  }
}
```

---

## Docker Compose

Add to `docker-compose.yml`:

```yaml
meilisearch:
  image: getmeili/meilisearch:v1.9
  environment:
    MEILI_MASTER_KEY: masterKey
    MEILI_NO_ANALYTICS: "true"
  ports:
    - "7700:7700"
  volumes:
    - meilisearch_data:/meili_data
  healthcheck:
    test: ["CMD-SHELL", "wget -qO- http://localhost:7700/health || exit 1"]
    interval: 5s
    timeout: 3s
    retries: 5
```

Add `meilisearch_data:` to the `volumes:` block.

No `profiles:` — MeiliSearch is a core runtime dependency when running the CardCatalog service.

---

## NuGet Packages

Add to `Directory.Packages.props`:

```xml
<PackageVersion Include="MeiliSearch" Version="0.19.0" />
```

Note: No dedicated `Testcontainers.MeiliSearch` package exists. The test project uses the generic `ContainerBuilder` API from the base `Testcontainers` package to spin up `getmeili/meilisearch:v1.9`. Add base package to `Directory.Packages.props`:

```xml
<PackageVersion Include="Testcontainers" Version="3.10.0" />
```

The test csproj references `Testcontainers` (base, for `ContainerBuilder`) and `Testcontainers.PostgreSql` (for `PostgreSqlContainer`).

Add to `src/CardCatalog/TCGTrading.CardCatalog.Api.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<PackageReference Include="MeiliSearch" />
```

---

## Tests

Project: `tests/CardCatalog.Tests/`

Target framework: `net10.0` (matches all other test projects)

Three integration tests using `WebApplicationFactory<Program>` + Testcontainers (one `Testcontainers.PostgreSql` container + one generic MeiliSearch container built with `ContainerBuilder`, shared across all tests via `IClassFixture`):

**Test 1 — Fuzzy name search:**
POST 5 seed cards → GET `/cards/search?q=drag` → result contains "Dragon Fire" and "Ancient Dragon" (fuzzy match on "drag").

**Test 2 — Filter by set and rarity:**
GET `/cards/search?set=Core%20Set%202024&rarity=Rare` → exactly 1 result: "Dragon Fire".

**Test 3 — Empty query returns all:**
GET `/cards/search` → total = 5, items.Length = 5.

The fixture overrides `CardDb` and `MeiliSearch:Host` connection strings to point at the Testcontainers instances.

---

## Clean Architecture Compliance

| Layer | What lives there |
|-------|-----------------|
| Domain | `Card`, `Rarity` — no infrastructure imports |
| Application | `ICardRepository`, `ICardSearchService`, DTOs — no infrastructure imports |
| Infrastructure | EF Core, MeiliSearch client — implements Application interfaces |
| Presentation (Program.cs) | Minimal API endpoints — calls interfaces only |

Zero cross-layer leakage. Application layer never imports `Meilisearch` or `EntityFrameworkCore` namespaces.

---

## Out of Scope

- Full-text search on other services (Portfolio, Marketplace) — each would be a separate feature
- MeiliSearch authentication beyond `masterKey` for local dev — prod key rotation is a deployment concern
- Pagination of the `POST /cards` import endpoint — not needed; seed is one-time
- Card update / delete endpoints — read-only catalog for now; add later with wishlist feature
- MeiliSearch index settings update idempotence — `UpdateAttributesAsync` is idempotent, so re-starting is safe
- Card image storage — separate "card image storage" feature (Azure Blob / MinIO)
