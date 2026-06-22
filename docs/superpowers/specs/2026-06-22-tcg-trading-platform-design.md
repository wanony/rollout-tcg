# TCG Trading Platform — Design Spec
_Date: 2026-06-22_

## Overview

A Pokemon TCG (extensible to any TCG) portfolio tracker and peer-to-peer marketplace. Users authenticate via OAuth2, track card holdings with real-time P&L, list cards for sale, make/accept offers, and receive live updates via SignalR. Built as true microservices with clean architecture per service and Docker Compose for local dev. Designed for zero-code-change migration to Azure.

---

## Goals

- Demonstrate event-driven microservices, OAuth2/OIDC, WebSockets (SignalR), clean architecture, and cloud-readiness in a single coherent portfolio project
- Memorable interview talking point (TCG domain) with serious technical depth
- Extensible TCG provider abstraction: Pokemon ships day one, MTG/YuGiOh add later with no core changes

---

## Services

| Service | Responsibility | DB |
|---|---|---|
| `identity-service` | Duende IdentityServer, user registration/login, OAuth2/OIDC token issuance | `db_identity` |
| `card-catalog-service` | `ITcgProvider` abstraction, Pokemon TCG API sync, card search/detail | `db_cards` |
| `portfolio-service` | User holdings, average cost basis, P&L calculation, consumes price + trade events | `db_portfolio` |
| `marketplace-service` | Listings, offers, purchases, ownership transfer event emission | `db_marketplace` |
| `pricing-service` | Scheduled TCG price API polling, publishes `CardPriceUpdated` events | `db_pricing` |
| `notification-service` | Consumes all integration events, pushes real-time updates via SignalR hub | in-memory + Redis backplane |
| `api-gateway` | YARP reverse proxy, JWT validation, routes all external traffic | none |

---

## TCG Provider Abstraction

Defined in `CardCatalog.Application` — infrastructure implements, no domain dependency on HTTP:

```csharp
public interface ITcgProvider
{
    string ProviderName { get; }
    Task<IEnumerable<Card>> SearchCardsAsync(string query, CancellationToken ct = default);
    Task<IEnumerable<Card>> GetSetCardsAsync(string setCode, CancellationToken ct = default);
    Task<CardPrice> GetPriceAsync(string providerCardId, CancellationToken ct = default);
}

// Registered implementations
public class PokemonTcgProvider : ITcgProvider { ... }   // ships day one
// public class MagicTcgProvider : ITcgProvider { ... }  // future
// public class YugiohTcgProvider : ITcgProvider { ... } // future
```

Provider selection via named DI registration — add new TCG by implementing interface + registering, zero changes to domain or application layers.

---

## Clean Architecture Per Service

Every service follows identical layer structure:

```
src/
  ServiceName/
    ServiceName.Domain/
      Entities/
      ValueObjects/
      Events/           ← domain events (internal)
      Exceptions/
      Interfaces/       ← repository contracts
    ServiceName.Application/
      Commands/         ← MediatR ICommand<Result<T>>
      Queries/          ← MediatR IQuery<Result<T>>
      EventHandlers/    ← integration event consumers
      Interfaces/       ← IMessageBus, IIntegrationEventPublisher
    ServiceName.Infrastructure/
      Persistence/      ← EF Core DbContext, repository implementations
      Messaging/        ← RabbitMQ or Azure Service Bus implementations
      Outbox/           ← outbox worker, OutboxMessage entity
      TcgProviders/     ← HTTP clients (card-catalog only)
    ServiceName.Api/
      Endpoints/        ← Minimal API route groups
      Middleware/
      Program.cs
```

### Domain Layer Principles

- Rich domain models — business rules enforced inside aggregates, not in application services
- Entities extend `AggregateRoot` base which holds `IReadOnlyList<IDomainEvent>`
- Value objects are immutable records: `Money`, `CardCondition`, `CardGrade`
- No dependencies on any other layer or external package (except primitives)

```csharp
public record Money(decimal Amount, string Currency = "USD")
{
    public Money Add(Money other)
    {
        if (Currency != other.Currency) throw new CurrencyMismatchException();
        return this with { Amount = Amount + other.Amount };
    }
}

// Grading service abstraction — new graders added without touching domain
public interface IGradingService
{
    string Name { get; }           // "PSA", "BGS", "CGC", "SGC", "ACE"
    bool IsValidGrade(decimal grade);
    string FormatGrade(decimal grade); // "PSA 10", "BGS 9.5"
}

// A holding is either raw/ungraded OR professionally graded — never both
public abstract record CardPhysicalCondition;

public record UngradedCondition(UngradedGrade Grade) : CardPhysicalCondition;
// NM, LP, MP, HP, Damaged

public record GradedCondition(
    string GradingService,      // "PSA", "BGS", "CGC", "SGC", "ACE"
    decimal Grade,              // 10, 9.5, 8 etc.
    string CertificationNumber  // unique cert # for authenticity verification
) : CardPhysicalCondition
{
    public string Display => $"{GradingService} {Grade}"; // "PSA 10"
}

public enum UngradedGrade { NearMint, LightlyPlayed, ModeratelyPlayed, HeavilyPlayed, Damaged }

public class Listing : AggregateRoot
{
    public Guid CardId { get; private set; }
    public Guid SellerId { get; private set; }
    public Money AskingPrice { get; private set; }
    public CardPhysicalCondition Condition { get; private set; }  // ungraded or graded
    public ListingStatus Status { get; private set; }

    public Result<Offer> AcceptOffer(Offer offer)
    {
        if (Status != ListingStatus.Active)
            return Result.Failure<Offer>("Listing is not active");

        Status = ListingStatus.Sold;
        AddDomainEvent(new ListingAcceptedDomainEvent(Id, offer.BuyerId, AskingPrice));
        return Result.Success(offer);
    }
}
```

### Application Layer Principles

- CQRS via MediatR — commands mutate state, queries read projections
- Commands return `Result<T>` — never throw for business rule failures
- Application layer depends only on domain + its own interfaces
- `IMessageBus` and `IIntegrationEventPublisher` abstractions live here

```csharp
public interface IMessageBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent;
    Task SubscribeAsync<T>(Func<T, Task> handler) where T : IntegrationEvent;
}

public interface IIntegrationEventPublisher
{
    Task PublishViaOutboxAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent;
}
```

### Reliable Event Publishing — Outbox Pattern

Every service that emits integration events uses the outbox:

1. Domain change + `OutboxMessage` insert = single DB transaction
2. `OutboxWorker` (background `IHostedService`) polls outbox, publishes to message broker, marks `ProcessedAt`
3. Consumers are idempotent (check `EventId` before processing)

```csharp
public class OutboxMessage
{
    public Guid Id { get; init; }
    public string EventType { get; init; }
    public string Payload { get; init; }       // JSON serialised IntegrationEvent
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
}
```

---

## Portfolio — Manual IRL Collection Entry

Users add cards from their physical collection manually. No external sync required. Each holding records condition precisely.

```csharp
public class Holding : AggregateRoot
{
    public Guid PortfolioId { get; private set; }
    public Guid CardId { get; private set; }             // references card-catalog
    public string CardName { get; private set; }         // denormalised for read performance
    public int Quantity { get; private set; }
    public Money CostBasis { get; private set; }         // what user paid (manual entry)
    public CardPhysicalCondition Condition { get; private set; }
    public DateTime AcquiredAt { get; private set; }
    public string? Notes { get; private set; }           // optional user notes
}
```

**Add to collection flow:**
1. User searches card catalog (card-catalog-service)
2. Selects card, enters quantity, cost paid, condition (ungraded grade OR grading service + grade + cert number)
3. `AddHoldingCommand` → portfolio-service → holding persisted
4. Pricing-service tracks card from this point, pushes market value updates via events

**Grading service extensibility:**

```csharp
// Registered in DI — add new grader with zero domain changes
public class PsaGradingService : IGradingService
{
    public string Name => "PSA";
    public bool IsValidGrade(decimal grade) => grade is >= 1 and <= 10 && grade % 0.5m == 0;
    public string FormatGrade(decimal grade) => $"PSA {grade}";
}

public class BgsGradingService : IGradingService   // Beckett
public class CgcGradingService : IGradingService   // CGC
public class SgcGradingService : IGradingService   // SGC
public class AceGradingService : IGradingService   // ACE (UK-based)
```

Grading service list fetched from DI at runtime — UI populates dropdown dynamically, no hardcoded strings in domain.

---

## Integration Events

All events inherit from:

```csharp
public abstract record IntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    string EventType);
```

| Event | Publisher | Consumers |
|---|---|---|
| `CardCatalogSynced` | card-catalog | pricing (register cards to track) |
| `CardPriceUpdated` | pricing | portfolio (recalc P&L), notification (push to clients) |
| `ListingCreated` | marketplace | notification (alert watchlist users) |
| `ListingPurchased` | marketplace | portfolio (transfer ownership), notification |
| `OfferMade` | marketplace | notification (alert seller) |
| `OfferAccepted` | marketplace | portfolio (transfer ownership), notification |

---

## Real-Time (SignalR)

`notification-service` hosts a single SignalR hub. Clients connect with JWT bearer token (SignalR supports token in query string for WebSocket handshake). Users joined to group keyed by `userId` for targeted pushes.

```csharp
public class TradingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnConnectedAsync();
    }
}
```

**Backplane:**
- Local: Redis (`AddStackExchangeRedis`) — supports multiple notification-service instances
- Azure: `AddAzureSignalR()` — one line swap in `Program.cs`

---

## API Gateway (YARP)

Single external entry point. Validates JWT against IdentityServer JWKS endpoint. Routes:

| Path prefix | Upstream |
|---|---|
| `/api/cards/**` | card-catalog-service |
| `/api/portfolio/**` | portfolio-service |
| `/api/marketplace/**` | marketplace-service |
| `/hubs/**` | notification-service (WebSocket passthrough) |
| `/connect/**` | identity-service (OAuth2 endpoints) |

---

## Infrastructure — Docker Compose (Local)

```yaml
services:
  rabbitmq:
    image: rabbitmq:3-management
    ports: ["5672:5672", "15672:15672"]

  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: dev
    volumes:
      - ./infra/postgres/init:/docker-entrypoint-initdb.d  # creates all DBs on first run

  redis:
    image: redis:7-alpine

  identity-service:
    build: ./src/IdentityService
    depends_on: [postgres]

  card-catalog-service:
    build: ./src/CardCatalog
    depends_on: [postgres, rabbitmq]

  portfolio-service:
    build: ./src/Portfolio
    depends_on: [postgres, rabbitmq]

  marketplace-service:
    build: ./src/Marketplace
    depends_on: [postgres, rabbitmq]

  pricing-service:
    build: ./src/Pricing
    depends_on: [postgres, rabbitmq]

  notification-service:
    build: ./src/Notification
    depends_on: [rabbitmq, redis]

  api-gateway:
    build: ./src/ApiGateway
    ports: ["8080:8080"]
    depends_on: [identity-service]
```

---

## Azure Migration Map

Zero code changes — only DI registration via environment config:

| Local | Azure |
|---|---|
| RabbitMQ | Azure Service Bus |
| Postgres (Docker) | Azure Database for PostgreSQL Flexible |
| Redis backplane | Azure SignalR Service |
| Duende IdentityServer (container) | Keep on Container App or swap to Azure AD B2C |
| YARP gateway | Keep YARP on Container Apps or Azure API Management |
| Docker Compose | Azure Container Apps |

---

## System Diagram

```
[Browser Client]
      │
      │ OAuth2 PKCE login
      ▼
[Identity Service — Duende IdentityServer]
      │
      │ JWT
      ▼
[API Gateway — YARP]  ← validates JWT, routes by path
      │
      ├──► [Card Catalog Service]
      ├──► [Portfolio Service]
      ├──► [Marketplace Service]
      └──► [Pricing Service]
                │
                │ publish via outbox
                ▼
        [RabbitMQ / Azure Service Bus]
                │
                ▼
        [Notification Service]
                │
                ▼
        [SignalR Hub] ──► real-time push ──► [Browser Client]
```

---

## External APIs

| API | Purpose | Rate limits |
|---|---|---|
| [Pokemon TCG API](https://pokemontcg.io/) | Card data, sets, images | 1000 req/day free, API key for more |
| [TCGPlayer API](https://docs.tcgplayer.com/) | Market prices | Requires partner account |
| Fallback: simulate prices locally | Dev/test without API key | n/a |

Price simulation mode (random walk on seed prices) enables full local dev without external API keys.

---

## Testing Strategy

- **Domain**: xUnit unit tests, no infrastructure deps
- **Application**: xUnit + NSubstitute for interface mocks
- **Integration**: Testcontainers (Postgres + RabbitMQ containers per test run)
- **E2E**: Not in scope for v1

---

## Out of Scope (v1)

- Real money / payment processing
- Mobile client
- Admin dashboard
- Card grading image upload / cert number verification against grading service APIs
- Auction-style timed bidding (add in v2 once marketplace stable)
- Gamified pack opening (planned for v2 — see project memory)
