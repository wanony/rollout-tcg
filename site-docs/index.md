# Rollout TCG

**Rollout TCG** is a full-stack microservices platform for trading physical Trading Card Game cards. Users can build and track their card portfolio, buy and sell on a live marketplace, and receive real-time push notifications when their listings sell or offers are made. A holographic React frontend surfaces price history charts, type-driven card glow effects, and an OIDC-secured auth flow.

The project is purpose-built to demonstrate production-grade .NET microservices architecture — distributed tracing, event-driven messaging, full-text search, time-series data, rate limiting, and a CI/CD pipeline that pushes container images to GHCR on every merge to `main`.

---

## Quick Links

| What | Where |
|---|---|
| Run it locally | [Quick Start](getting-started/quick-start.md) |
| Develop locally | [Local Development](getting-started/development.md) |
| Try the demo account | [Demo Account](getting-started/demo.md) |
| Architecture diagram | [Architecture Overview](architecture/overview.md) |
| Grafana observability | [Observability](infrastructure/observability.md) |
| API endpoints | Each [service page](services/identity.md) has its own endpoint reference |

---

## Platform Overview

```mermaid
graph TD
    subgraph client["Client"]
        Browser(["Browser · React · SignalR"])
    end

    subgraph auth["Auth"]
        IS["Identity Service :5001<br/>Duende IdentityServer · PKCE"]
    end

    subgraph gateway["Gateway"]
        GW["API Gateway :5010<br/>YARP · Redis rate limiting"]
    end

    subgraph services["Backend Services"]
        CC["Card Catalog :5005<br/>MeiliSearch full-text search"]
        MKT["Marketplace :5003<br/>listings · purchases"]
        PRT["Portfolio :5002<br/>collection tracking"]
        PRC["Pricing :5006<br/>TimescaleDB time-series"]
        NTF["Notification :5004<br/>SignalR push"]
    end

    subgraph bus["Event Bus"]
        MQ[("RabbitMQ 3.13<br/>MassTransit")]
    end

    subgraph data["Data Stores"]
        PG[("PostgreSQL 16<br/>5 service DBs")]
        TS[("TimescaleDB<br/>pricing history")]
        MS[("MeiliSearch<br/>card index")]
        RD[("Redis 7<br/>rate limiting")]
    end

    subgraph obs["Observability ⟨--profile observability⟩"]
        OTL["OTel Collector"]
        GRF["Grafana · Tempo · Prometheus · Loki"]
    end

    Browser -- "OIDC / PKCE" --> IS
    Browser -- "REST" --> GW
    Browser <-. "SignalR WebSocket" .-> NTF
    GW --> CC & MKT & PRT & PRC & NTF
    GW --> RD

    MKT -- "ListingCreated<br/>ListingPurchased" --> MQ
    MQ --> NTF & PRT

    IS & CC & MKT & PRT & NTF --> PG
    PRC --> TS
    CC --> MS

    services & GW -- "OTLP" --> OTL
    OTL --> GRF
```

---

## Tech Stack at a Glance

| Layer | Technology |
|---|---|
| **Runtime** | .NET 10 · C# 13 · ASP.NET Core Minimal APIs |
| **Auth** | Duende IdentityServer 7 · ASP.NET Core Identity · PKCE |
| **Messaging** | MassTransit 8 + RabbitMQ 3.13 |
| **ORM** | EF Core 10 + Npgsql (5 services) · Dapper (Pricing) |
| **Search** | MeiliSearch 1.9 |
| **Gateway** | YARP 2.3 |
| **Rate Limiting** | RedisRateLimiting.AspNetCore + StackExchange.Redis |
| **Observability** | OpenTelemetry SDK · Serilog · Grafana / Tempo / Prometheus / Loki |
| **Frontend** | React 18 · TypeScript · Vite · Tailwind CSS · framer-motion |
| **CI / CD** | GitHub Actions → GHCR |
| **Testing** | xUnit · FluentAssertions · NSubstitute · Testcontainers |
