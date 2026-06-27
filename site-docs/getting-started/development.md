# Local Development

This page covers running services outside of Docker for fast iteration — edit code, hot-reload, see the change immediately.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — for infrastructure (Postgres, Redis, RabbitMQ, etc.)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm — for the frontend

## Start infrastructure only

Spin up databases, RabbitMQ, Redis, and MeiliSearch without the .NET services:

```bash
docker compose up -d postgres timescaledb rabbitmq redis meilisearch
```

Services will bind on their default ports. See [Docker Compose](../infrastructure/docker-compose.md) for the full port list.

## Build and test the solution

```bash
# Build the whole solution
dotnet build TCGTrading.sln

# Run all tests
# Testcontainers spins up real Postgres, RabbitMQ, Redis, and MeiliSearch containers
# — no external infrastructure needed for tests
dotnet test TCGTrading.sln
```

!!! note "Test containers"
    Tests are fully self-contained via Testcontainers. They pull Docker images on first run, which adds a few seconds. Subsequent runs reuse the pulled images.

## Run individual services

```bash
# Portfolio service (connects to postgres on localhost:5432)
dotnet run --project src/Portfolio

# Marketplace service
dotnet run --project src/Marketplace

# Card Catalog (also needs MeiliSearch on localhost:7700)
dotnet run --project src/CardCatalog

# Identity Service (also needs postgres db_identity)
dotnet run --project src/IdentityService
```

Each service reads its connection strings from `appsettings.json` → `appsettings.Development.json`. The Development overrides point at `localhost` rather than the Docker service hostnames.

## Frontend dev server

```bash
cd src/Frontend
npm install
npm run dev
```

The Vite dev server starts on `http://localhost:3000`. All `/api/*` requests are proxied to the API Gateway at `http://localhost:5010` via `vite.config.ts`.

The OIDC callback URL `http://localhost:3000/callback` is pre-registered as a valid redirect URI in the IdentityServer `spa` client — no config changes needed.

```bash
# Type check without building
npm run tsc

# Run frontend unit tests
npm test

# Build production bundle
npm run build
```

## Solution structure

```
rollout-tcg/
  src/
    ApiGateway/         # YARP reverse proxy + rate limiting
    CardCatalog/        # MeiliSearch-backed card search
    Frontend/           # React 18 + Vite SPA
    IdentityService/    # Duende IdentityServer + ASP.NET Core Identity
    Marketplace/        # Listings, offers, purchases
    Notification/       # RabbitMQ consumer + SignalR hub
    Portfolio/          # Collection tracking
    Pricing/            # TimescaleDB price history
    SharedKernel/       # Domain primitives + OTel extensions
  tests/
    ApiGateway.Tests/
    CardCatalog.Tests/
    IdentityService.Tests/
    Marketplace.Tests/
    Notification.Tests/
    Portfolio.Tests/
    Pricing.Tests/
    SharedKernel.Tests/
  infra/
    grafana/            # Provisioned dashboards and datasources
    loki/               # Log aggregation config
    otel-collector/     # OTLP collector config
    postgres/           # DB init SQL
    prometheus/         # Scrape config
    tempo/              # Trace storage config
  docs/
    superpowers/        # Implementation plans and specs (internal)
  site-docs/            # MkDocs source (this site)
  docker-compose.yml
  mkdocs.yml
  TCGTrading.sln
  Directory.Build.props   # Global: net10.0, nullable enable, warnings as errors
  Directory.Packages.props # Central package version management
```

## Central package management

All NuGet package versions are declared once in `Directory.Packages.props`. Individual `.csproj` files reference packages **without** a `Version` attribute:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="MassTransit" Version="8.3.6" />

<!-- src/Marketplace/Marketplace.csproj -->
<PackageReference Include="MassTransit" />  <!-- version resolved centrally -->
```

This prevents version drift across services. Add a new package by adding its version to `Directory.Packages.props` first.

## Useful commands

```bash
# Add an EF Core migration to a service
dotnet ef migrations add <Name> --project src/Portfolio --startup-project src/Portfolio

# Apply migrations manually
dotnet ef database update --project src/Portfolio

# List migration status
dotnet ef migrations list --project src/Portfolio
```

!!! note "Auto-migration"
    All services call `db.Database.MigrateAsync()` at startup, so migrations apply automatically when a service starts. Manual `dotnet ef database update` is only needed for inspection.
