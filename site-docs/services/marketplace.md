# Marketplace Service

**Port:** 5003  
**Database:** `db_marketplace` (PostgreSQL)  
**Technology:** EF Core 10 + Npgsql, MassTransit (publisher)

The Marketplace Service handles the buying and selling of cards. Sellers create fixed-price listings; buyers purchase them. The service publishes integration events to RabbitMQ so downstream services (Notification, Portfolio) can react without a direct dependency.

## Domain model

```
Listing (AggregateRoot)
  Id: Guid
  SellerId: Guid
  CardId: string           pokemontcg.io card ID
  CardName: string
  Condition: string
  AskingPriceUsd: decimal
  Status: ListingStatus    Active | Sold | Cancelled
  BuyerId: Guid?           set on purchase
  PurchasedAt: DateTime?   set on purchase
  CreatedAt: DateTime
```

### State machine

```
Active ──purchase()──▶ Sold
Active ──cancel()───▶ Cancelled
Sold   ──(any)──────▶ InvalidOperationException
```

Transitions are enforced in the domain entity — the API layer calls `listing.Purchase(buyerId)` and catches `InvalidOperationException` to return a 409 Conflict.

## Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/listings` | Create a new listing |
| `GET` | `/listings` | Get all active listings |
| `GET` | `/listings/{id}` | Get a specific listing |
| `POST` | `/listings/{id}/purchase` | Purchase a listing |
| `DELETE` | `/listings/{id}` | Cancel a listing |
| `GET` | `/health` | Health check |

### Create a listing

```bash
curl -X POST http://localhost:5003/listings \
  -H "Content-Type: application/json" \
  -d '{
    "sellerId": "00000000-0000-0000-0000-000000000001",
    "cardId": "base1-4",
    "cardName": "Charizard",
    "condition": "Near Mint",
    "askingPriceUsd": 500.00
  }'
```

### Purchase a listing

```bash
curl -X POST http://localhost:5003/listings/{id}/purchase \
  -H "Content-Type: application/json" \
  -d '{ "buyerId": "00000000-0000-0000-0000-000000000002" }'
```

Returns 200 with the updated listing, or 409 Conflict if already sold or cancelled.

## Integration events published

| Event | When |
|---|---|
| `ListingCreatedEvent` | After a new listing is created |
| `ListingPurchasedEvent` | After a listing is successfully purchased |
| `OfferMadeEvent` | After an offer is submitted (offer flow) |
| `OfferAcceptedEvent` | After a seller accepts an offer |

Events are published directly in the HTTP handler using `IPublishEndpoint`:

```csharp
await publish.Publish(new ListingPurchasedEvent(
    Guid.NewGuid(), DateTime.UtcNow,
    listing.Id, listing.CardId,
    request.BuyerId, listing.SellerId, listing.AskingPriceUsd), ct);
```

!!! note "Reliability"
    Events are currently published directly (no outbox). If the broker is unavailable at publish time the event is lost — the DB write has already succeeded. Wiring up the SharedKernel outbox pattern is a planned improvement.

## Testing

`tests/Marketplace.Tests/` uses `WebApplicationFactory<Program>` with `Testcontainers.PostgreSql` and `Testcontainers.RabbitMq`. Tests cover the full listing lifecycle including event publishing verification via MassTransit's test harness.
