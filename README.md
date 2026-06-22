# Rollout TCG Trading Platform

A microservices platform for trading physical TCG cards — portfolio tracking, marketplace listings, real-time pricing, and OAuth2-secured APIs. Built with .NET 8 and Duende IdentityServer.

## Services

| Service | Port | Status | Description |
|---|---|---|---|
| Identity Service | 5001 | ✅ Built | OAuth2/OIDC token issuance, user registration |
| Card Catalog | — | 🔨 Planned | Card data via Pokémon TCG API, sync events |
| Portfolio | — | 🔨 Planned | Holdings, grading, manual entry, P&L |
| Pricing | — | 🔨 Planned | Scheduled price polling, `CardPriceUpdated` events |
| Marketplace | — | 🔨 Planned | Listings, offers, purchases |
| Notification | — | 🔨 Planned | SignalR push notifications |
| API Gateway | — | 🔨 Planned | YARP reverse proxy with JWT validation |

## Infrastructure

| Component | Port | Purpose |
|---|---|---|
| PostgreSQL 16 | 5432 | Per-service databases (`db_identity`, etc.) |
| RabbitMQ 3.13 | 5672 / 15672 | Integration events via MassTransit |
| Redis 7 | 6379 | Caching / session state |

## Tech Stack

- **Runtime:** .NET 8, ASP.NET Core, C# 12
- **Auth:** Duende IdentityServer 7, ASP.NET Core Identity
- **Messaging:** MassTransit + RabbitMQ
- **Database:** EF Core 8 + PostgreSQL (Npgsql)
- **Architecture:** CQRS (MediatR), Domain Events, Transactional Outbox
- **Testing:** xUnit, FluentAssertions, Testcontainers

## Quick Start

**Prerequisites:** Docker Desktop, .NET SDK 10 (targets net8.0)

```bash
# Start infrastructure + identity service
docker compose up -d

# Verify identity service is healthy
curl http://localhost:5001/.well-known/openid-configuration
```

**Register a user:**

```bash
curl -X POST http://localhost:5001/account/register \
  -H "Content-Type: application/json" \
  -d '{"userName":"trainername","email":"trainer@pokecenter.com","password":"Pikachu@1"}'
```

**Get a token (integration/dev use only):**

```bash
curl -X POST http://localhost:5001/connect/token \
  -d "grant_type=password&client_id=test-client&client_secret=test-secret&username=trainername&password=Pikachu@1&scope=openid profile tcg.full"
```

## Development

```bash
# Build
dotnet build TCGTrading.sln

# Run all tests
dotnet test TCGTrading.sln

# Run identity service locally (requires postgres running)
dotnet run --project src/IdentityService
```

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      API Gateway                        │
│              (YARP + JWT validation)                    │
└──────────┬──────────────────────────┬───────────────────┘
           │                          │
    ┌──────▼──────┐            ┌──────▼──────────────┐
    │  Identity   │            │   Service Mesh       │
    │  Service    │            │  Card Catalog        │
    │  (OAuth2)   │            │  Portfolio           │
    └─────────────┘            │  Pricing             │
                               │  Marketplace         │
                               │  Notification        │
                               └──────────────────────┘
                                         │
                    ┌────────────────────┼────────────────┐
                    │                    │                 │
             ┌──────▼──────┐   ┌────────▼──────┐  ┌──────▼──────┐
             │ PostgreSQL  │   │   RabbitMQ    │  │    Redis    │
             │ (per-svc DB)│   │ (MassTransit) │  │  (cache)    │
             └─────────────┘   └───────────────┘  └─────────────┘
```

## SharedKernel

Cross-cutting types shared by all services:

- **Domain:** `AggregateRoot`, `Entity`, `IDomainEvent`, value objects (`Money`, `CardPhysicalCondition`, grading types)
- **Contracts:** Integration events — `CardCatalogSyncedEvent`, `CardPriceUpdatedEvent`, `ListingCreatedEvent`, `ListingPurchasedEvent`, `OfferMadeEvent`, `OfferAcceptedEvent`
- **Infrastructure:** Transactional outbox (`OutboxMessage`, `OutboxWorkerBase`, `IOutboxRepository`)

## OAuth2 Clients

| Client | Grant | Use |
|---|---|---|
| `spa` | Authorization Code + PKCE | Frontend SPA (no client secret) |
| `test-client` | Resource Owner Password | Integration tests only — disabled in Production |

API scope: `tcg.full`

## Project Structure

```
src/
  SharedKernel/        # Domain primitives, events, outbox
  IdentityService/     # Duende IS + ASP.NET Identity
  CardCatalog/         # (planned)
  Portfolio/           # (planned)
  Pricing/             # (planned)
  Marketplace/         # (planned)
  Notification/        # (planned)
  ApiGateway/          # (planned)
tests/
  SharedKernel.Tests/
  IdentityService.Tests/
infra/
  postgres/            # DB init scripts (creates per-service databases)
  rabbitmq/            # RabbitMQ config
```
