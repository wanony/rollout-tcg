# Demo Account

The platform ships with a pre-seeded demo account so you can explore every feature without registering or adding data manually.

## Credentials

| Field | Value |
|---|---|
| **Email** | `demo@rollout.dev` |
| **Password** | `Demo1234!` |

## What's pre-seeded

The demo account has a realistic dataset loaded at startup:

- **Portfolio** — a collection of real Pokémon TCG cards spanning multiple sets and rarities, each with a realistic acquisition price in USD
- **Marketplace listings** — several active listings at market price, so the marketplace page is never empty
- **Price history** — the Pricing service seeds 150 synthetic price-point rows per seeded card, so the sparkline charts have meaningful data to display

## Logging in

1. Open [http://localhost](http://localhost)
2. Click **Sign In** in the navbar
3. You are redirected to the Identity Service login page
4. Enter `demo@rollout.dev` and `Demo1234!`
5. After authentication, you return to the SPA with a valid session

## How seeding works

Seeding is handled by hosted services that run at startup:

- **IdentityService** — a `SeedHostedService` checks whether `demo@rollout.dev` exists and creates it with ASP.NET Core Identity if absent; idempotent on every restart
- **Portfolio** — a `PortfolioSeedService` adds a fixed set of cards to the demo user's collection if the collection is empty
- **Marketplace** — a `MarketplaceSeedService` creates active listings for a subset of the demo user's cards

The seed data uses real pokemontcg.io card IDs so images, names, and types resolve correctly in the frontend.

## Getting an API token (dev only)

For direct API testing without the browser PKCE flow, use the `test-client` resource owner password grant:

```bash
curl -X POST http://localhost:5001/connect/token \
  -d "grant_type=password&client_id=test-client&client_secret=test-secret&username=demo@rollout.dev&password=Demo1234!&scope=openid profile tcg.full"
```

The response contains an `access_token` you can use as a `Bearer` token against any service endpoint.

!!! warning "Dev-only grant"
    The `test-client` Resource Owner Password grant is disabled in production environments. It exists only for integration tests and local API exploration. The SPA always uses the `spa` client with Authorization Code + PKCE.

## Registering a new account

To test the registration flow, visit [http://localhost:5001/Account/Register](http://localhost:5001/Account/Register) directly on the Identity Service, or use the API:

```bash
curl -X POST http://localhost:5001/account/register \
  -H "Content-Type: application/json" \
  -d '{"userName":"ash","email":"ash@pokecenter.com","password":"Pikachu@1"}'
```
