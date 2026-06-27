# Shared Kernel

`src/SharedKernel` is a C# class library referenced by all services. It contains only shared primitives, contracts, and infrastructure utilities — no business logic. It is a project reference, not a NuGet package; all services in this solution reference it directly.

## Domain primitives

### Entity

```csharp
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected init; } = DateTime.UtcNow;
}
```

All domain objects that have a stable identity inherit from `Entity`. The ID and creation timestamp are set once and never mutated.

### AggregateRoot

```csharp
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public IReadOnlyList<IDomainEvent> PopDomainEvents()
    {
        var events = _domainEvents.ToList().AsReadOnly();
        _domainEvents.Clear();
        return events;
    }
}
```

`AggregateRoot` extends `Entity` with a domain event collection. Aggregates raise domain events internally using `AddDomainEvent()`; the application layer calls `PopDomainEvents()` after persisting to get and publish them.

### IDomainEvent

Marker interface — lets the application layer collect and dispatch events without knowing their concrete type.

### Result&lt;T&gt;

```csharp
var result = Result<CollectionItem>.Success(item);
var failure = Result<CollectionItem>.Failure("Quantity must be positive.");

if (result.IsFailure)
    return Results.BadRequest(new { error = result.Error });

var item = result.Value; // throws if IsFailure
```

A discriminated success/failure type for use cases that need to return errors without throwing exceptions. Accessing `.Value` on a failed result or `.Error` on a successful result throws — this makes misuse a programmer error caught at dev time.

## Value objects

| Type | Purpose |
|---|---|
| `Money` | Amount + currency code; throws `CurrencyMismatchException` on cross-currency operations |
| `CardPhysicalCondition` | Wrapper for graded and ungraded condition variants |
| `GradedCondition` | PSA/BGS numeric grade (1–10) |
| `UngradedCondition` | Collector-grade string (Near Mint, Lightly Played, etc.) |

## Integration event contracts

All cross-service events live here so publishers and consumers share the same type without a direct project reference to each other.

| Event | Publisher | Consumers |
|---|---|---|
| `ListingCreatedEvent` | Marketplace | Notification |
| `ListingPurchasedEvent` | Marketplace | Notification, Portfolio |
| `OfferMadeEvent` | Marketplace | Notification |
| `OfferAcceptedEvent` | Marketplace | Notification |
| `CardCatalogSyncedEvent` | Card Catalog | — |
| `CardPriceUpdatedEvent` | Pricing | — |

All events extend `IntegrationEvent` (a base record with `EventId: Guid` and `OccurredAt: DateTime`) and are positional immutable records.

## Infrastructure utilities

### TelemetryExtensions.AddTelemetry(serviceName)

One call configures all three observability signals:

```csharp
// Program.cs of any service
builder.AddTelemetry("marketplace");
```

What it registers:

- **Serilog** — structured JSON logs to stdout and Loki, enriched with the active trace span ID (so you can jump from a log line to its trace in Grafana)
- **OTel traces** — exports to the OTel Collector via OTLP over gRPC; instruments HttpClient, EF Core, and MassTransit automatically
- **OTel metrics** — exports to the OTel Collector; instruments HttpClient and .NET runtime (GC, thread pool, etc.)

Each service then adds its own ASP.NET Core instrumentation and the Prometheus scrape endpoint locally.

### Outbox

The SharedKernel provides the plumbing for the transactional outbox pattern:

| Type | Purpose |
|---|---|
| `OutboxMessage` | EF Core entity representing a pending event row |
| `IOutboxRepository` | Interface for writing and reading outbox messages |
| `OutboxWorkerBase` | Abstract `BackgroundService` that polls for undelivered messages and relays them to the broker |

Individual services opt in by implementing a concrete `OutboxRepository` and `OutboxWorker`. Currently the Marketplace publishes events directly in the HTTP handler; wiring up the outbox is a planned reliability improvement.
