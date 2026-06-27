# Docker Compose

The entire stack runs via `docker compose up -d`. The observability tools (Grafana, Tempo, Prometheus, Loki) are gated behind a `--profile observability` flag so they don't start unless explicitly requested.

## Starting the stack

```bash
# Core stack (all services + infrastructure)
docker compose up -d

# Full stack including observability
docker compose --profile observability up -d

# Individual service (useful during development)
docker compose up -d postgres rabbitmq redis meilisearch
```

## Service inventory

### Infrastructure

| Service | Image | Port | Profile |
|---|---|---|---|
| `postgres` | `postgres:16` | 5432 | default |
| `timescaledb` | `timescale/timescaledb:latest-pg16` | 5433 | default |
| `rabbitmq` | `rabbitmq:3.13-management` | 5672, 15672 | default |
| `redis` | `redis:7` | 6379 | default |
| `meilisearch` | `getmeili/meilisearch:v1.9` | 7700 | default |

### Application services

| Service | Built from | Port | Profile |
|---|---|---|---|
| `identity-service` | `src/IdentityService` | 5001 | default |
| `portfolio` | `src/Portfolio` | 5002 | default |
| `marketplace` | `src/Marketplace` | 5003 | default |
| `notification` | `src/Notification` | 5004 | default |
| `card-catalog` | `src/CardCatalog` | 5005 | default |
| `pricing` | `src/Pricing` | 5006 | default |
| `api-gateway` | `src/ApiGateway` | 5010 | default |
| `frontend` | `src/Frontend` | 80 | default |

### Observability

| Service | Image | Port | Profile |
|---|---|---|---|
| `otel-collector` | `otel/opentelemetry-collector-contrib:0.104.0` | 4317, 4318 | observability |
| `tempo` | `grafana/tempo:2.5.0` | 3200 | observability |
| `prometheus` | `prom/prometheus:v2.53.0` | 9090 | observability |
| `loki` | `grafana/loki:3.1.0` | 3100 | observability |
| `grafana` | `grafana/grafana:11.1.0` | 3001 | observability |

## Health checks

PostgreSQL, RabbitMQ, and Redis have health checks. Application services wait for their dependencies to be healthy before starting:

```yaml
# Example: Marketplace waits for Postgres and RabbitMQ
depends_on:
  postgres:
    condition: service_healthy
  rabbitmq:
    condition: service_healthy
```

## Database initialisation

`infra/postgres/init/01-create-databases.sql` runs once when the Postgres container is first created. It creates all service databases:

```sql
CREATE DATABASE db_identity;
CREATE DATABASE db_portfolio;
CREATE DATABASE db_marketplace;
CREATE DATABASE db_notification;
CREATE DATABASE db_cardcatalog;
```

EF Core migrations run at service startup — they apply schema changes on top of these empty databases.

## Volumes

Named volumes persist data across container restarts:

| Volume | Used by |
|---|---|
| `postgres_data` | postgres |
| `timescale_data` | timescaledb |
| `rabbitmq_data` | rabbitmq |
| `redis_data` | redis |
| `meilisearch_data` | meilisearch |
| `grafana_data` | grafana |
| `tempo_data` | tempo |
| `loki_data` | loki |

## Resetting the stack

```bash
# Stop everything and remove volumes (full reset)
docker compose --profile observability down -v

# Then start fresh
docker compose up -d
```

!!! warning "Data loss"
    `-v` deletes all named volumes, wiping every database and the MeiliSearch index. Seed data will be re-applied on next startup, but any user-created data (accounts, portfolios, listings) will be gone.

## override file

`docker-compose.override.yml` contains developer-specific overrides (environment variables for local tooling, port forwarding adjustments). This file is git-ignored in the generated pattern but committed here for convenience.
