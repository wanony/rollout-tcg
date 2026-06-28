# Portfolio Service

**Port:** 5002  
**Database:** `db_portfolio` (PostgreSQL)  
**Technology:** EF Core 10 + Npgsql, MassTransit (consumer only)

The Portfolio Service manages each user's card collection. Users can add cards with condition and acquisition price, view their collection, and see a cost summary. The service also listens for `ListingPurchasedEvent` to automatically add cards to the buyer's portfolio when a marketplace purchase completes.

## Domain model

```
CollectionItem (AggregateRoot)
  Id: Guid
  UserId: Guid
  CardId: string          pokemontcg.io card ID (e.g., "base1-4")
  CardName: string
  Quantity: int           min 1
  Condition: string       e.g., "Near Mint", "Lightly Played"
  AcquisitionPriceUsd: decimal
  CreatedAt: DateTime
```

## Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/portfolio/cards` | Add a card to the collection |
| `GET` | `/portfolio/cards?userId={guid}` | List all cards for a user |
| `GET` | `/portfolio/summary?userId={guid}` | Total cost and item count |
| `DELETE` | `/portfolio/cards/{id}` | Remove a card from the collection |
| `GET` | `/health` | Health check |

### Add a card

```bash
curl -X POST http://localhost:5002/portfolio/cards \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "00000000-0000-0000-0000-000000000001",
    "cardId": "base1-4",
    "cardName": "Charizard",
    "quantity": 1,
    "condition": "Near Mint",
    "acquisitionPriceUsd": 450.00
  }'
```

### Get collection

```bash
curl "http://localhost:5002/portfolio/cards?userId=00000000-0000-0000-0000-000000000001"
```

### Portfolio summary

```json
{
  "userId": "00000000-0000-0000-0000-000000000001",
  "itemCount": 12,
  "totalAcquisitionCostUsd": 1234.56
}
```

## Event consumption

The service registers a MassTransit consumer for `ListingPurchasedEvent`:

```csharp
// When a purchase completes in Marketplace, Portfolio auto-adds the card
public class ListingPurchasedConsumer : IConsumer<ListingPurchasedEvent>
{
    public async Task Consume(ConsumeContext<ListingPurchasedEvent> context)
    {
        var ev = context.Message;
        var item = CollectionItem.Create(
            ev.BuyerId, ev.CardId, ev.CardName, 1, "Unknown", ev.PriceUsd);
        await _repo.AddAsync(item, context.CancellationToken);
    }
}
```

The buyer's portfolio is updated asynchronously — the purchase API call returns immediately, and the portfolio addition happens within seconds.

## Testing

`tests/Portfolio.Tests/` uses `WebApplicationFactory<Program>` with `Testcontainers.PostgreSql`. Tests cover adding cards, listing, summary calculation, and the `ListingPurchasedEvent` consumer in isolation via NSubstitute mocks.
