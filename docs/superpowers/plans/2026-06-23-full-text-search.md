# Full-Text Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the CardCatalog stub with a real service — cards persisted in PostgreSQL, indexed in MeiliSearch, and searchable via `GET /cards/search` with fuzzy matching and filters.

**Architecture:** Clean architecture layers in CardCatalog (Domain → Application → Infrastructure → Program.cs). EF Core writes cards to Postgres; on each write (and at startup) the card is also indexed in MeiliSearch. Search reads from MeiliSearch only. Application layer defines `ICardRepository` and `ICardSearchService` interfaces; Infrastructure provides the adapters.

**Tech Stack:** `MeiliSearch` 0.19.0 (.NET SDK), EF Core 8.0 + Npgsql, Testcontainers generic container for MeiliSearch in tests, Testcontainers.PostgreSql for tests.

## Global Constraints

- Clean arch: Domain layer has zero infrastructure imports; Application layer has zero `Meilisearch.*` or `EntityFrameworkCore.*` imports
- `MeiliSearch` NuGet package version: 0.19.0 (NuGet ID is `MeiliSearch`, namespace is `Meilisearch`, client class is `MeilisearchClient`)
- MeiliSearch index name: `cards`; primary key field: `id` (lowercase, string)
- Rarity stored as `int` in Postgres; as enum member name string in MeiliSearch (e.g., `"Rare"`, `"SecretRare"`)
- Searchable attributes (order = relevance weight): `name`, `type`, `set`, `text`
- Filterable attributes: `set`, `rarity`, `type`
- Pagination: `page` is 1-based; MeiliSearch `offset = (page - 1) * pageSize`, `limit = pageSize`
- Config key for DB: `ConnectionStrings:CardDb`; production host: `postgres:5432`; dev host: `localhost:5432`
- Config key for search: `MeiliSearch:Host` and `MeiliSearch:ApiKey`; production host: `http://meilisearch:7700`; API key: `masterKey`
- Test project target framework: `net10.0`; `NuGetAuditMode=direct`; `test.runsettings` with `DOTNET_ROLL_FORWARD=LatestMajor`
- `TreatWarningsAsErrors=true` — zero warnings allowed
- Seed data: exactly 5 cards (Dragon Fire, Shadow Knight, Forest Guardian, Lightning Bolt, Ancient Dragon) — see Task 3 for exact values
- `public partial class Program {}` required in `src/CardCatalog/Program.cs` for `WebApplicationFactory<Program>` in tests

---

## File Map

**New files:**
```
src/CardCatalog/Domain/Entities/Card.cs                               — Task 2
src/CardCatalog/Domain/Enums/Rarity.cs                                — Task 2
src/CardCatalog/Application/Interfaces/ICardRepository.cs             — Task 2
src/CardCatalog/Application/Interfaces/ICardSearchService.cs          — Task 2
src/CardCatalog/Application/DTOs/CardSearchResult.cs                  — Task 2
src/CardCatalog/Application/DTOs/CardSearchResponse.cs                — Task 2
src/CardCatalog/Infrastructure/Persistence/CardCatalogDbContext.cs    — Task 3
src/CardCatalog/Infrastructure/Persistence/CardRepository.cs          — Task 3
src/CardCatalog/Infrastructure/Persistence/PersistenceExtensions.cs   — Task 3
src/CardCatalog/Infrastructure/Persistence/Migrations/               — Task 3
src/CardCatalog/Infrastructure/Seed/CardSeed.cs                       — Task 3
src/CardCatalog/Infrastructure/Search/MeiliSearchCardSearchService.cs — Task 4
src/CardCatalog/Infrastructure/Search/SearchExtensions.cs             — Task 4
tests/CardCatalog.Tests/TCGTrading.CardCatalog.Api.Tests.csproj       — Task 5
tests/CardCatalog.Tests/test.runsettings                               — Task 5
tests/CardCatalog.Tests/GlobalUsings.cs                                — Task 5
tests/CardCatalog.Tests/Fixtures/CardCatalogFixture.cs                — Task 5
tests/CardCatalog.Tests/Integration/SearchIntegrationTests.cs         — Task 5
```

**Modified files:**
```
Directory.Packages.props                     — Task 1 (add MeiliSearch, Testcontainers)
src/CardCatalog/TCGTrading.CardCatalog.Api.csproj — Task 1 (add EF Core, Npgsql, MeiliSearch)
docker-compose.yml                           — Task 1 (add meilisearch service + volume)
src/CardCatalog/appsettings.json             — Task 1 (add ConnectionStrings + MeiliSearch sections)
src/CardCatalog/appsettings.Development.json — Task 1 (add local connection strings)
src/CardCatalog/Program.cs                   — Task 5 (AddPersistence + AddSearch + partial Program, no endpoints yet)
                                               Task 6 (add startup sequence + endpoints)
TCGTrading.sln                               — Task 5 (add CardCatalog.Tests project)
```

---

### Task 1: Package setup and configuration

Add NuGet packages, add MeiliSearch to docker-compose, update appsettings. No implementation.

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/CardCatalog/TCGTrading.CardCatalog.Api.csproj`
- Modify: `docker-compose.yml`
- Modify: `src/CardCatalog/appsettings.json`
- Modify: `src/CardCatalog/appsettings.Development.json`

**Interfaces:**
- Produces: all packages available for Tasks 2–5; MeiliSearch container available in docker-compose; config keys present

- [ ] **Step 1: Add package versions to `Directory.Packages.props`**

Open `Directory.Packages.props`. Add these entries inside the existing `<ItemGroup>`, after the `<!-- Test infrastructure -->` section:

```xml
<!-- Search -->
<PackageVersion Include="MeiliSearch" Version="0.19.0" />
<!-- Testcontainers base (generic container API for MeiliSearch in tests) -->
<PackageVersion Include="Testcontainers" Version="3.10.0" />
```

- [ ] **Step 2: Add package references to CardCatalog csproj**

Open `src/CardCatalog/TCGTrading.CardCatalog.Api.csproj`. Replace the full file with:

```xml
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
    <PackageReference Include="MeiliSearch" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add MeiliSearch service to `docker-compose.yml`**

Open `docker-compose.yml`. Add the `meilisearch` service block after the `redis:` block:

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

Add `meilisearch_data:` to the `volumes:` block at the bottom of the file (alongside `postgres_data:`, `rabbitmq_data:`, etc.).

- [ ] **Step 4: Update `src/CardCatalog/appsettings.json`**

Replace the full file:

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
    "CardDb": "Host=postgres;Port=5432;Database=db_cards;Username=tcg;Password=dev"
  },
  "MeiliSearch": {
    "Host": "http://meilisearch:7700",
    "ApiKey": "masterKey"
  }
}
```

- [ ] **Step 5: Update `src/CardCatalog/appsettings.Development.json`**

Replace the full file:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Telemetry": {
    "OtlpEndpoint": "http://localhost:4317",
    "LokiEndpoint": "http://localhost:3100"
  },
  "ConnectionStrings": {
    "CardDb": "Host=localhost;Port=5432;Database=db_cards;Username=tcg;Password=dev"
  },
  "MeiliSearch": {
    "Host": "http://localhost:7700",
    "ApiKey": "masterKey"
  }
}
```

- [ ] **Step 6: Verify restore**

```bash
dotnet restore TCGTrading.sln
```

Expected: `Restore complete.` with zero errors.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props src/CardCatalog/TCGTrading.CardCatalog.Api.csproj \
        docker-compose.yml src/CardCatalog/appsettings.json \
        src/CardCatalog/appsettings.Development.json
git commit -m "feat(search): add MeiliSearch and EF Core packages and config"
```

---

### Task 2: Domain and Application layer

Pure types — no infrastructure imports anywhere. Domain has the `Card` entity and `Rarity` enum. Application has the interfaces and DTOs.

**Files:**
- Create: `src/CardCatalog/Domain/Entities/Card.cs`
- Create: `src/CardCatalog/Domain/Enums/Rarity.cs`
- Create: `src/CardCatalog/Application/Interfaces/ICardRepository.cs`
- Create: `src/CardCatalog/Application/Interfaces/ICardSearchService.cs`
- Create: `src/CardCatalog/Application/DTOs/CardSearchResult.cs`
- Create: `src/CardCatalog/Application/DTOs/CardSearchResponse.cs`

**Interfaces:**
- Produces: `Card`, `Rarity`, `ICardRepository`, `ICardSearchService`, `CardSearchResult`, `CardSearchResponse` — used by Tasks 3, 4, 5, 6

- [ ] **Step 1: Create `src/CardCatalog/Domain/Enums/Rarity.cs`**

```csharp
namespace TCGTrading.CardCatalog.Domain.Enums;

public enum Rarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    UltraRare = 3,
    SecretRare = 4
}
```

- [ ] **Step 2: Create `src/CardCatalog/Domain/Entities/Card.cs`**

`Card` uses a private constructor + static factory to ensure it's always created with valid data. EF Core uses the private constructor via reflection.

```csharp
using TCGTrading.CardCatalog.Domain.Enums;

namespace TCGTrading.CardCatalog.Domain.Entities;

public sealed class Card
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Set { get; private set; } = string.Empty;
    public Rarity Rarity { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;

    private Card() { }

    public static Card Create(string name, string set, Rarity rarity, string type, string text)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            Name = name,
            Set = set,
            Rarity = rarity,
            Type = type,
            Text = text
        };
    }
}
```

- [ ] **Step 3: Create `src/CardCatalog/Application/Interfaces/ICardRepository.cs`**

```csharp
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Application.Interfaces;

public interface ICardRepository
{
    Task<Card> AddAsync(Card card, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> GetAllAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Create `src/CardCatalog/Application/DTOs/CardSearchResult.cs`**

```csharp
namespace TCGTrading.CardCatalog.Application.DTOs;

public sealed record CardSearchResult(
    Guid Id,
    string Name,
    string Set,
    string Rarity,
    string Type,
    string Text);
```

- [ ] **Step 5: Create `src/CardCatalog/Application/DTOs/CardSearchResponse.cs`**

```csharp
namespace TCGTrading.CardCatalog.Application.DTOs;

public sealed record CardSearchResponse(
    IReadOnlyList<CardSearchResult> Items,
    long Total,
    int Page,
    int PageSize);
```

- [ ] **Step 6: Create `src/CardCatalog/Application/Interfaces/ICardSearchService.cs`**

```csharp
using TCGTrading.CardCatalog.Application.DTOs;
using TCGTrading.CardCatalog.Domain.Entities;

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

- [ ] **Step 7: Verify build**

```bash
dotnet build src/CardCatalog/TCGTrading.CardCatalog.Api.csproj -c Release -q
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 8: Commit**

```bash
git add src/CardCatalog/Domain/ src/CardCatalog/Application/
git commit -m "feat(search): add Card domain entity and application interfaces"
```

---

### Task 3: EF Core persistence and seed data

`CardCatalogDbContext`, `CardRepository`, `PersistenceExtensions`, EF Core migration, and `CardSeed`.

**Files:**
- Create: `src/CardCatalog/Infrastructure/Persistence/CardCatalogDbContext.cs`
- Create: `src/CardCatalog/Infrastructure/Persistence/CardRepository.cs`
- Create: `src/CardCatalog/Infrastructure/Persistence/PersistenceExtensions.cs`
- Create: `src/CardCatalog/Infrastructure/Persistence/Migrations/20260623100000_InitialCreate.cs`
- Create: `src/CardCatalog/Infrastructure/Persistence/Migrations/CardCatalogDbContextModelSnapshot.cs`
- Create: `src/CardCatalog/Infrastructure/Seed/CardSeed.cs`

**Interfaces:**
- Consumes: `Card` (Task 2), `ICardRepository` (Task 2)
- Produces: `CardCatalogDbContext`, `CardRepository`, `PersistenceExtensions.AddPersistence()`, `CardSeed.Cards`

- [ ] **Step 1: Create `src/CardCatalog/Infrastructure/Persistence/CardCatalogDbContext.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Infrastructure.Persistence;

public sealed class CardCatalogDbContext(DbContextOptions<CardCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<Card> Cards => Set<Card>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.Property(c => c.Set).IsRequired().HasMaxLength(200);
            e.Property(c => c.Rarity).HasConversion<int>();
            e.Property(c => c.Type).IsRequired().HasMaxLength(100);
            e.Property(c => c.Text).IsRequired().HasMaxLength(2000);
        });
    }
}
```

- [ ] **Step 2: Create `src/CardCatalog/Infrastructure/Persistence/CardRepository.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using TCGTrading.CardCatalog.Application.Interfaces;
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Infrastructure.Persistence;

public sealed class CardRepository(CardCatalogDbContext db) : ICardRepository
{
    public async Task<Card> AddAsync(Card card, CancellationToken ct = default)
    {
        db.Cards.Add(card);
        await db.SaveChangesAsync(ct);
        return card;
    }

    public async Task<IReadOnlyList<Card>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Cards.AsNoTracking().ToListAsync(ct);
    }
}
```

- [ ] **Step 3: Create `src/CardCatalog/Infrastructure/Persistence/PersistenceExtensions.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using TCGTrading.CardCatalog.Application.Interfaces;

namespace TCGTrading.CardCatalog.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("CardDb")
            ?? throw new InvalidOperationException("ConnectionStrings:CardDb is not configured.");

        builder.Services.AddDbContext<CardCatalogDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<ICardRepository, CardRepository>();

        return builder;
    }
}
```

- [ ] **Step 4: Create `src/CardCatalog/Infrastructure/Seed/CardSeed.cs`**

```csharp
using TCGTrading.CardCatalog.Domain.Entities;
using TCGTrading.CardCatalog.Domain.Enums;

namespace TCGTrading.CardCatalog.Infrastructure.Seed;

public static class CardSeed
{
    public static IReadOnlyList<Card> Cards { get; } =
    [
        Card.Create("Dragon Fire",       "Core Set 2024",   Rarity.Rare,       "Spell",    "Deal 3 damage to any target."),
        Card.Create("Shadow Knight",     "Core Set 2024",   Rarity.Uncommon,   "Creature", "First strike. When ~ enters, scry 1."),
        Card.Create("Forest Guardian",   "Wilds of Nature", Rarity.Common,     "Creature", "Reach. Whenever ~ blocks, gain 2 life."),
        Card.Create("Lightning Bolt",    "Starter Pack",    Rarity.Common,     "Spell",    "Deal 1 damage to any target. Fast."),
        Card.Create("Ancient Dragon",    "Wilds of Nature", Rarity.SecretRare, "Creature", "Flying, trample. ETB: deal 5 to each opponent."),
    ];
}
```

- [ ] **Step 5: Create the EF Core migration files**

Create `src/CardCatalog/Infrastructure/Persistence/Migrations/20260623100000_InitialCreate.cs`:

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCGTrading.CardCatalog.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Cards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Set = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Rarity = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cards", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Cards");
    }
}
```

Create `src/CardCatalog/Infrastructure/Persistence/Migrations/CardCatalogDbContextModelSnapshot.cs`:

```csharp
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TCGTrading.CardCatalog.Infrastructure.Persistence;

#nullable disable

namespace TCGTrading.CardCatalog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CardCatalogDbContext))]
partial class CardCatalogDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("TCGTrading.CardCatalog.Domain.Entities.Card", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<int>("Rarity")
                .HasColumnType("integer");

            b.Property<string>("Set")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<string>("Text")
                .IsRequired()
                .HasMaxLength(2000)
                .HasColumnType("character varying(2000)");

            b.Property<string>("Type")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.HasKey("Id");

            b.ToTable("Cards");
        });
#pragma warning restore 612, 618
    }
}
```

- [ ] **Step 6: Verify build**

```bash
dotnet build src/CardCatalog/TCGTrading.CardCatalog.Api.csproj -c Release -q
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 7: Commit**

```bash
git add src/CardCatalog/Infrastructure/Persistence/ src/CardCatalog/Infrastructure/Seed/
git commit -m "feat(search): add EF Core persistence layer and seed data"
```

---

### Task 4: MeiliSearch infrastructure

`MeiliSearchCardSearchService` and `SearchExtensions`. Implements `ICardSearchService`.

**Files:**
- Create: `src/CardCatalog/Infrastructure/Search/MeiliSearchCardSearchService.cs`
- Create: `src/CardCatalog/Infrastructure/Search/SearchExtensions.cs`

**Interfaces:**
- Consumes: `ICardSearchService` (Task 2), `CardSearchResult` (Task 2), `CardSearchResponse` (Task 2), `Card` (Task 2), `MeilisearchClient` from `Meilisearch` namespace
- Produces: `MeiliSearchCardSearchService`, `SearchExtensions.AddSearch(IHostApplicationBuilder)`

- [ ] **Step 1: Create `src/CardCatalog/Infrastructure/Search/MeiliSearchCardSearchService.cs`**

Key design notes:
- MeiliSearch documents store rarity as string (`card.Rarity.ToString()` = `"Rare"`, `"SecretRare"`, etc.)
- `IndexAllAsync` calls `WaitForTaskAsync` after `AddDocumentsAsync` so that documents are searchable immediately — critical for tests that index then immediately search
- `EnsureIndexAsync` creates the index (idempotent) and configures searchable/filterable attributes on every call — idempotent in MeiliSearch (same settings = no-op)
- `IndexAsync` (single card add) does NOT wait for the task — fire-and-forget is fine for individual writes; startup's `IndexAllAsync` uses wait to guarantee test correctness

```csharp
using Meilisearch;
using Microsoft.Extensions.Logging;
using TCGTrading.CardCatalog.Application.DTOs;
using TCGTrading.CardCatalog.Application.Interfaces;
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Infrastructure.Search;

public sealed class MeiliSearchCardSearchService(
    MeilisearchClient client,
    ILogger<MeiliSearchCardSearchService> logger) : ICardSearchService
{
    private const string IndexName = "cards";

    public async Task IndexAsync(Card card, CancellationToken ct = default)
    {
        try
        {
            var index = await EnsureIndexAsync(ct);
            await index.AddDocumentsAsync([ToDocument(card)], cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index card {CardId} ({CardName})", card.Id, card.Name);
        }
    }

    public async Task IndexAllAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        if (cards.Count == 0) return;
        try
        {
            var index = await EnsureIndexAsync(ct);
            var taskInfo = await index.AddDocumentsAsync(
                cards.Select(ToDocument), cancellationToken: ct);
            // Wait for indexing to complete — ensures search works immediately after startup
            await client.WaitForTaskAsync(taskInfo.TaskUid, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk index {Count} cards", cards.Count);
        }
    }

    public async Task<CardSearchResponse> SearchAsync(
        string? query,
        string? set,
        string? rarity,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var index = client.Index(IndexName);

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(set))
            filters.Add($"set = \"{set}\"");
        if (!string.IsNullOrEmpty(rarity))
            filters.Add($"rarity = \"{rarity}\"");

        var searchQuery = new SearchQuery
        {
            Filter = filters.Count > 0 ? string.Join(" AND ", filters) : null,
            Offset = (page - 1) * pageSize,
            Limit = pageSize,
        };

        var result = await index.SearchAsync<CardDocument>(
            query ?? string.Empty, searchQuery, ct);

        var items = result.Hits.Select(h => new CardSearchResult(
            Guid.Parse(h.id),
            h.name,
            h.set,
            h.rarity,
            h.type,
            h.text)).ToList();

        return new CardSearchResponse(
            items,
            result.EstimatedTotalHits ?? items.Count,
            page,
            pageSize);
    }

    private async Task<Index> EnsureIndexAsync(CancellationToken ct)
    {
        await client.CreateIndexAsync(IndexName, "id", ct);
        var index = client.Index(IndexName);
        await index.UpdateSearchableAttributesAsync(
            ["name", "type", "set", "text"], ct);
        await index.UpdateFilterableAttributesAsync(
            ["set", "rarity", "type"], ct);
        return index;
    }

    private static CardDocument ToDocument(Card card) => new(
        card.Id.ToString(),
        card.Name,
        card.Set,
        card.Rarity.ToString(),
        card.Type,
        card.Text);

    // Lowercase field names — MeiliSearch primary key is "id"; field names must match serialized JSON keys
    private sealed record CardDocument(
        string id, string name, string set, string rarity, string type, string text);
}
```

- [ ] **Step 2: Create `src/CardCatalog/Infrastructure/Search/SearchExtensions.cs`**

```csharp
using Meilisearch;
using TCGTrading.CardCatalog.Application.Interfaces;

namespace TCGTrading.CardCatalog.Infrastructure.Search;

public static class SearchExtensions
{
    public static IHostApplicationBuilder AddSearch(this IHostApplicationBuilder builder)
    {
        var host = builder.Configuration["MeiliSearch:Host"]
            ?? throw new InvalidOperationException("MeiliSearch:Host is not configured.");
        var apiKey = builder.Configuration["MeiliSearch:ApiKey"] ?? "masterKey";

        builder.Services.AddSingleton(_ => new MeilisearchClient(host, apiKey));
        builder.Services.AddScoped<ICardSearchService, MeiliSearchCardSearchService>();

        return builder;
    }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/CardCatalog/TCGTrading.CardCatalog.Api.csproj -c Release -q
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add src/CardCatalog/Infrastructure/Search/
git commit -m "feat(search): add MeiliSearch search service and DI extension"
```

---

### Task 5: Test project and failing tests

Create the test project, the `CardCatalogFixture` (Testcontainers Postgres + MeiliSearch), and 3 integration tests. Also update `Program.cs` to wire `AddPersistence()` and `AddSearch()` and add `public partial class Program {}` — this lets the test host start. Tests fail at runtime with 404 because no endpoints exist yet.

**Files:**
- Modify: `src/CardCatalog/Program.cs` — add AddPersistence + AddSearch + `public partial class Program {}`
- Create: `tests/CardCatalog.Tests/TCGTrading.CardCatalog.Api.Tests.csproj`
- Create: `tests/CardCatalog.Tests/test.runsettings`
- Create: `tests/CardCatalog.Tests/GlobalUsings.cs`
- Create: `tests/CardCatalog.Tests/Fixtures/CardCatalogFixture.cs`
- Create: `tests/CardCatalog.Tests/Integration/SearchIntegrationTests.cs`
- Modify: `TCGTrading.sln` — add test project

**Interfaces:**
- Consumes: `Program` (via `WebApplicationFactory<Program>`), `ICardRepository` (Task 2), `ICardSearchService` (Task 2), `CardCatalogDbContext` (Task 3)
- Produces: 3 failing tests (404 responses) that Task 6 will make pass

- [ ] **Step 1: Update `src/CardCatalog/Program.cs`**

Replace the full file with this skeleton — wires services but has no endpoints yet and no startup sequence. This is intentionally incomplete; Task 6 adds the rest.

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.CardCatalog.Infrastructure.Persistence;
using TCGTrading.CardCatalog.Infrastructure.Search;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("card-catalog");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();
builder.AddSearch();

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
```

- [ ] **Step 2: Create `tests/CardCatalog.Tests/TCGTrading.CardCatalog.Api.Tests.csproj`**

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
    <PackageReference Include="Testcontainers" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <ProjectReference Include="../../src/CardCatalog/TCGTrading.CardCatalog.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `tests/CardCatalog.Tests/test.runsettings`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <DOTNET_ROLL_FORWARD>LatestMajor</DOTNET_ROLL_FORWARD>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

- [ ] **Step 4: Create `tests/CardCatalog.Tests/GlobalUsings.cs`**

```csharp
global using Xunit;
```

- [ ] **Step 5: Create `tests/CardCatalog.Tests/Fixtures/CardCatalogFixture.cs`**

The fixture starts Testcontainers (Postgres + MeiliSearch) and overrides config via `ConfigureAppConfiguration`. It does NOT reference concrete types from the production infrastructure — only the Application interfaces needed by `WebApplicationFactory`.

```csharp
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace TCGTrading.CardCatalog.Tests;

public class CardCatalogFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("db_cards")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    private readonly IContainer _meilisearch = new ContainerBuilder()
        .WithImage("getmeili/meilisearch:v1.9")
        .WithEnvironment("MEILI_MASTER_KEY", "masterKey")
        .WithEnvironment("MEILI_NO_ANALYTICS", "true")
        .WithPortBinding(7700, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(7700))
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _meilisearch.StartAsync());
    }

    public new async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _meilisearch.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CardDb"] = _postgres.GetConnectionString(),
                ["MeiliSearch:Host"] = $"http://localhost:{_meilisearch.GetMappedPublicPort(7700)}",
                ["MeiliSearch:ApiKey"] = "masterKey",
            });
        });
    }
}
```

- [ ] **Step 6: Create `tests/CardCatalog.Tests/Integration/SearchIntegrationTests.cs`**

Tests will fail with 404 until Task 6 adds the endpoints. The 404 failures confirm the test harness is correctly wired.

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.CardCatalog.Application.DTOs;

namespace TCGTrading.CardCatalog.Tests.Integration;

public class SearchIntegrationTests(CardCatalogFixture fixture)
    : IClassFixture<CardCatalogFixture>
{
    [Fact]
    public async Task FuzzyNameSearch_Returns_MatchingCards()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/cards/search?q=drag");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "fuzzy search for 'drag' should match 'Dragon Fire' and 'Ancient Dragon'");
        var body = await response.Content.ReadFromJsonAsync<CardSearchResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        body.Items.Should().Contain(i =>
            i.Name.Contains("Dragon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FilterBySetAndRarity_Returns_SingleMatch()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync(
            "/cards/search?set=Core%20Set%202024&rarity=Rare");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CardSearchResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(1);
        body.Items[0].Name.Should().Be("Dragon Fire");
    }

    [Fact]
    public async Task EmptyQuery_Returns_AllCards()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/cards/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CardSearchResponse>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(5);
        body.Items.Should().HaveCount(5);
    }
}
```

- [ ] **Step 7: Add test project to solution**

```bash
dotnet sln TCGTrading.sln add tests/CardCatalog.Tests/TCGTrading.CardCatalog.Api.Tests.csproj \
    --solution-folder tests/CardCatalog.Tests
```

- [ ] **Step 8: Verify tests compile and fail at runtime**

```bash
dotnet test tests/CardCatalog.Tests/TCGTrading.CardCatalog.Api.Tests.csproj
```

Expected: Build succeeds. All 3 tests FAIL — `Expected HttpStatusCode.OK but found NotFound`. This confirms the test harness is wired correctly and the tests are waiting for the endpoints.

- [ ] **Step 9: Commit**

```bash
git add src/CardCatalog/Program.cs tests/CardCatalog.Tests/ TCGTrading.sln
git commit -m "test(search): add CardCatalog.Tests project with 3 failing integration tests"
```

---

### Task 6: Endpoints and startup wiring — makes tests pass

Complete `Program.cs`: add the startup sequence (migrate DB, seed, index) and the two API endpoints. After this task, 3/3 tests pass.

**Files:**
- Modify: `src/CardCatalog/Program.cs` — add startup sequence + `GET /cards/search` + `POST /cards`

**Interfaces:**
- Consumes: `ICardRepository` (Task 2), `ICardSearchService` (Task 2), `CardCatalogDbContext` (Task 3), `CardSeed` (Task 3), `Card.Create()` (Task 2), `CardSearchResponse` (Task 2)
- Produces: working `GET /cards/search` and `POST /cards` endpoints; all 3 integration tests pass

- [ ] **Step 1: Replace `src/CardCatalog/Program.cs` with full implementation**

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.CardCatalog.Application.DTOs;
using TCGTrading.CardCatalog.Application.Interfaces;
using TCGTrading.CardCatalog.Domain.Entities;
using TCGTrading.CardCatalog.Domain.Enums;
using TCGTrading.CardCatalog.Infrastructure.Persistence;
using TCGTrading.CardCatalog.Infrastructure.Search;
using TCGTrading.CardCatalog.Infrastructure.Seed;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("card-catalog");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();
builder.AddSearch();

var app = builder.Build();

// Apply EF migrations, seed cards if empty, sync MeiliSearch index
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CardCatalogDbContext>();
    await db.Database.MigrateAsync();

    var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
    var search = scope.ServiceProvider.GetRequiredService<ICardSearchService>();

    var existing = await repo.GetAllAsync();
    if (existing.Count == 0)
    {
        foreach (var card in CardSeed.Cards)
            await repo.AddAsync(card);
        existing = await repo.GetAllAsync();
    }

    await search.IndexAllAsync(existing);
}

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.MapGet("/cards/search", async (
    ICardSearchService search,
    string? q,
    string? set,
    string? rarity,
    int page = 1,
    int pageSize = 20,
    CancellationToken ct = default) =>
{
    pageSize = Math.Min(pageSize, 100);
    var result = await search.SearchAsync(q, set, rarity, page, pageSize, ct);
    return Results.Ok(result);
});

app.MapPost("/cards", async (
    CreateCardRequest request,
    ICardRepository repo,
    ICardSearchService search,
    CancellationToken ct) =>
{
    var card = Card.Create(request.Name, request.Set, request.Rarity, request.Type, request.Text);
    await repo.AddAsync(card, ct);
    await search.IndexAsync(card, ct);
    return Results.Created($"/cards/{card.Id}", card);
});

app.Run();

public partial class Program { }

public sealed record CreateCardRequest(
    string Name,
    string Set,
    Rarity Rarity,
    string Type,
    string Text);
```

- [ ] **Step 2: Run the CardCatalog integration tests**

```bash
dotnet test tests/CardCatalog.Tests/TCGTrading.CardCatalog.Api.Tests.csproj -v normal
```

Expected:
```
Passed  FuzzyNameSearch_Returns_MatchingCards
Passed  FilterBySetAndRarity_Returns_SingleMatch
Passed  EmptyQuery_Returns_AllCards

Test Run Successful.
Total tests: 3
     Passed: 3
     Failed: 0
```

If fuzzy search test fails: MeiliSearch's fuzzy matching ("drag" → "Dragon") uses typo tolerance which is enabled by default. If it's not matching, try `q=dragon` instead — but it should work.

If the filter test fails: ensure the `rarity` filter uses the string `"Rare"` not `"2"`. The `CardDocument` stores `card.Rarity.ToString()` = `"Rare"`.

- [ ] **Step 3: Run full test suite for regressions**

```bash
dotnet test TCGTrading.sln --no-build -c Release
```

Expected: all tests passing (38 existing + 3 new = 41 total).

- [ ] **Step 4: Commit**

```bash
git add src/CardCatalog/Program.cs
git commit -m "feat(search): wire endpoints and startup sequence — CardCatalog search live"
```

---

## Summary

| Task | Deliverable | Verified by |
|------|-------------|-------------|
| 1 | Packages + docker-compose + config | `dotnet restore` succeeds |
| 2 | Domain entity + application interfaces | `dotnet build` succeeds, no infra imports |
| 3 | EF Core persistence + migration + seed | `dotnet build` succeeds |
| 4 | MeiliSearch service + DI extension | `dotnet build` succeeds |
| 5 | Test project + 3 failing tests | Tests compile, fail with 404 |
| 6 | Endpoints + startup sequence | 3/3 tests pass, 41/41 total suite passes |
