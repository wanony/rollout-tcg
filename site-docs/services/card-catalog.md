# Card Catalog Service

**Port:** 5005  
**Database:** `db_cardcatalog` (PostgreSQL) + MeiliSearch index `cards`  
**Technology:** EF Core 10 + Npgsql, MeiliSearch .NET SDK 0.19

The Card Catalog Service stores the full card dataset in PostgreSQL and maintains a MeiliSearch index for fast, typo-tolerant full-text search. Writes go to Postgres; searches read from MeiliSearch only.

## Domain model

```
Card (Entity)
  Id: string            pokemontcg.io card ID (e.g., "base1-4")
  Name: string
  Type: string          e.g., "Fire", "Water", "Psychic"
  Set: string           e.g., "Base Set", "Scarlet & Violet"
  Rarity: int           enum: Common=0, Uncommon=1, Rare=2, RareHolo=3, SecretRare=4
  Text: string          card flavour / attack text
  ImageUrl: string      pokemontcg.io image URL
```

## Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/cards/search` | Full-text search with filters and pagination |
| `GET` | `/cards/{id}` | Get a single card by ID |
| `POST` | `/cards` | Upsert a card (also indexes into MeiliSearch) |
| `GET` | `/health` | Health check |

### Search

```
GET /cards/search?q=charizard&set=Base+Set&rarity=RareHolo&type=Fire&page=1&pageSize=20
```

| Query param | Type | Description |
|---|---|---|
| `q` | string | Free-text search query (typo-tolerant) |
| `set` | string | Filter by set name |
| `rarity` | string | Filter by rarity |
| `type` | string | Filter by card type |
| `page` | int | 1-based page number (default: 1) |
| `pageSize` | int | Results per page (default: 20, max: 100) |

```json
{
  "results": [
    {
      "id": "base1-4",
      "name": "Charizard",
      "type": "Fire",
      "set": "Base Set",
      "rarity": "RareHolo",
      "imageUrl": "https://images.pokemontcg.io/base1/4_hires.png"
    }
  ],
  "totalHits": 3,
  "page": 1,
  "pageSize": 20
}
```

## MeiliSearch configuration

**Index name:** `cards`  
**Primary key:** `id`  
**Searchable attributes** (relevance ordered): `name`, `type`, `set`, `text`  
**Filterable attributes:** `set`, `rarity`, `type`

At startup, the service:
1. Ensures the `cards` index exists in MeiliSearch
2. Configures searchable and filterable attributes
3. Syncs all existing Postgres cards to the index (handles the case where MeiliSearch data was wiped)

On each `POST /cards`, the card is written to Postgres first, then indexed into MeiliSearch. If MeiliSearch indexing fails, the Postgres write is retained and will be synced at next startup.

## Rarity storage

Rarity is stored as an integer enum in Postgres for efficient querying, but as its member name string in MeiliSearch for human-readable filtering:

| Postgres value | MeiliSearch value |
|---|---|
| 0 | `"Common"` |
| 1 | `"Uncommon"` |
| 2 | `"Rare"` |
| 3 | `"RareHolo"` |
| 4 | `"SecretRare"` |

## Testing

`tests/CardCatalog.Tests/` uses `Testcontainers.PostgreSql` and a generic Testcontainers container for MeiliSearch. Tests cover upsert, search, filter combinations, and startup sync behaviour.
