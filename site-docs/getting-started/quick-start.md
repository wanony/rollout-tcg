# Quick Start

Get the full Rollout TCG stack running locally in under five minutes.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) 4.x+
- [.NET 10 SDK](https://dotnet.microsoft.com/download) — only needed if you want to run services outside of Docker

## Run the stack

```bash
# Clone the repo
git clone https://github.com/wanony/rollout-tcg.git
cd rollout-tcg

# Start all services
docker compose up -d

# Optional: add the observability stack (Grafana, Tempo, Prometheus, Loki)
docker compose --profile observability up -d
```

Docker Compose will start every service in dependency order. The first run pulls images and builds the frontend — expect 3–5 minutes. Subsequent starts are seconds.

```bash
# Check all services are healthy
docker compose ps
```

## Local URLs

Once everything is up:

| URL | What |
|---|---|
| **[http://localhost](http://localhost)** | Frontend SPA — start here |
| **[http://localhost:3001](http://localhost:3001)** | Grafana dashboards *(observability profile)* |
| [http://localhost:15672](http://localhost:15672) | RabbitMQ management UI (guest / guest) |
| [http://localhost:7700](http://localhost:7700) | MeiliSearch dashboard |
| [http://localhost:5001](http://localhost:5001) | Identity Service |
| [http://localhost:5010](http://localhost:5010) | API Gateway |
| [http://localhost:9090](http://localhost:9090) | Prometheus *(observability profile)* |

## Demo account

A pre-seeded demo account lets you explore the platform without registering:

| Field | Value |
|---|---|
| Email | `demo@rollout.dev` |
| Password | `Demo1234!` |

The demo account has an existing portfolio with real cards and active marketplace listings.

## Smoke tests

```bash
# OIDC discovery document
curl http://localhost:5001/.well-known/openid-configuration

# Card search
curl "http://localhost:5005/cards/search?q=charizard&pageSize=3"

# Health checks
curl http://localhost:5010/health
curl http://localhost:5002/health
curl http://localhost:5003/health
```

## Stopping the stack

```bash
docker compose --profile observability down

# Remove volumes (wipes all DB data)
docker compose --profile observability down -v
```

!!! warning "Volume data"
    Running with `-v` deletes all PostgreSQL, TimescaleDB, Redis, and MeiliSearch data. The seed data will be re-applied on next startup, but any user-created data is gone.
