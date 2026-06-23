# Observability Stack — Design Spec

**Date:** 2026-06-22  
**Status:** Approved  
**Scope:** OpenTelemetry instrumentation across all 7 .NET services + Grafana/Tempo/Prometheus/Loki in docker-compose

---

## Goal

Add production-grade observability to Rollout TCG: distributed tracing, metrics, and structured logs — all queryable from a single Grafana UI. Activated via `docker compose --profile observability up`.

---

## Architecture

### Signal flow

```
Services → OTLP gRPC (:4317) → otel-collector ──► Tempo        (traces)
                                                ──► Prometheus  (metrics, remote-write)
                                                ──► Loki        (logs, via Loki exporter)

Grafana ← queries Tempo + Prometheus + Loki
```

Prometheus also scrapes `/metrics` on each service (pull model, fallback/redundancy).

### docker-compose profile

All observability containers use `profiles: [observability]`. Activated with:

```bash
docker compose --profile observability up
```

| Container | Image | Ports | Role |
|-----------|-------|-------|------|
| `otel-collector` | `otel/opentelemetry-collector-contrib:latest` | 4317 (gRPC), 4318 (HTTP) | OTLP receiver, fan-out |
| `tempo` | `grafana/tempo:latest` | 3200 | Trace storage |
| `prometheus` | `prom/prometheus:latest` | 9090 | Metrics storage |
| `loki` | `grafana/loki:latest` | 3100 | Log aggregation |
| `grafana` | `grafana/grafana:latest` | 3000 | Unified dashboard UI |

All containers use named volumes. Grafana datasources and dashboards provisioned automatically on startup — no manual UI configuration required.

---

## Clean Architecture Constraints

Telemetry is an infrastructure concern. Strict layer rules:

| Layer | OTel knowledge | Logging |
|-------|---------------|---------|
| Domain | None | None |
| Application | None | `ILogger<T>` only |
| Infrastructure | Owns `ActivitySource`, span creation, Serilog config | Full |
| Presentation | None | Auto-instrumented by ASP.NET Core |

Manual span interfaces are defined in the Application layer (no OTel imports). Implementations live in Infrastructure.

---

## SharedKernel: `AddTelemetry` Extension

**File:** `src/SharedKernel/Infrastructure/Telemetry/TelemetryExtensions.cs`

```csharp
public static IHostApplicationBuilder AddTelemetry(
    this IHostApplicationBuilder builder, string serviceName)
```

Configures once, for all services:

### Logging — Serilog + Loki

- Serilog replaces default .NET logging
- Enriched with `TraceId`, `SpanId`, `ServiceName`, `Environment`
- Sinks: structured JSON console + Grafana Loki (push)
- Log level from `appsettings.json` / env vars

### Traces — OTLP → otel-collector → Tempo

Auto-instrumented:
- `OpenTelemetry.Instrumentation.EntityFrameworkCore`
- `OpenTelemetry.Instrumentation.Http` (HttpClient)
- MassTransit — emits activities via `ActivitySource` named `"MassTransit"`; registered with `.AddSource("MassTransit")` in tracing builder

Service name and version set via `ResourceBuilder`.

### Metrics — OTLP + Prometheus scrape endpoint

- OTLP metrics exported to collector
- `/metrics` endpoint exposed on each service (Prometheus pull)
- .NET runtime metrics: GC collections, heap size, thread pool, exception rate

### Configuration

```json
"Telemetry": {
  "OtlpEndpoint": "http://otel-collector:4317",
  "LokiEndpoint": "http://loki:3100",
  "LogLevel": "Information"
}
```

Env var override: `Telemetry__OtlpEndpoint=...`

---

## Per-Service Wiring

All 7 services use `WebApplication.CreateBuilder` — all get ASP.NET Core instrumentation.

```csharp
// Program.cs (every service)
builder.AddTelemetry("pricing"); // service name varies
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation());
```

---

## Manual Business Spans

Manual spans follow the interface-in-Application / impl-in-Infrastructure pattern. Pricing service is the reference implementation.

### Application layer interface

**File:** `src/Pricing/Application/Interfaces/IPricingTelemetry.cs`

```csharp
public interface IPricingTelemetry
{
    IDisposable? StartPriceCalculation(string cardId, string source);
    IDisposable? StartMarketDataFetch(string provider);
}
```

No `using OpenTelemetry` — zero OTel dependency in Application layer.

### Infrastructure implementation

**File:** `src/Pricing/Infrastructure/Telemetry/PricingTelemetry.cs`

```csharp
public sealed class PricingTelemetry : IPricingTelemetry
{
    private static readonly ActivitySource Source = new("tcgtrading.pricing", "1.0.0");

    public IDisposable? StartPriceCalculation(string cardId, string source) =>
        Source.StartActivity("pricing.calculate")
            ?.SetTag("card.id", cardId)
            .SetTag("pricing.source", source);

    public IDisposable? StartMarketDataFetch(string provider) =>
        Source.StartActivity("pricing.market_data_fetch")
            ?.SetTag("market.provider", provider);
}
```

Application handlers inject `IPricingTelemetry` via constructor. DI registers the Infrastructure impl.

As other services gain real business logic, they follow the same pattern with their own `I<Service>Telemetry` / `<Service>Telemetry` pair.

---

## Infrastructure Config Files

### `infra/otel-collector/config.yaml`

- Receiver: OTLP gRPC + HTTP
- Processors: batch, memory_limiter, resource detection
- Exporters: Tempo (traces), Prometheus remote-write (metrics), Loki (logs)

### `infra/tempo/tempo.yaml`

- Local filesystem backend (dev)
- OTLP gRPC receiver enabled

### `infra/prometheus/prometheus.yml`

- Scrape all services every 15s via `/metrics`
- Remote-write receiver enabled (from collector)

### `infra/grafana/provisioning/`

- `datasources/datasources.yaml` — Tempo, Prometheus, Loki pre-wired
- `dashboards/dashboards.yaml` — dashboard loader config

---

## Grafana Dashboards

Three pre-provisioned dashboards (JSON in `infra/grafana/dashboards/`):

| Dashboard | Key panels |
|-----------|-----------|
| `services-overview.json` | Request rate, error rate, p50/p95/p99 latency per service |
| `infrastructure.json` | RabbitMQ queue depth, Postgres pool, Redis hits/misses, .NET runtime (GC, heap, threadpool) |
| `traces.json` | Tempo trace search by service / operation / status code / duration |

---

## NuGet Packages

Added to `Directory.Packages.props` (central version management):

```xml
<!-- OTel core -->
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.9.0-rc.1" />

<!-- Instrumentation -->
<PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />

<!-- Serilog -->
<PackageVersion Include="Serilog.AspNetCore" Version="8.0.2" />
<PackageVersion Include="Serilog.Enrichers.Span" Version="3.1.0" />
<PackageVersion Include="Serilog.Sinks.Grafana.Loki" Version="8.3.0" />
```

SharedKernel references OTel core + instrumentation + Serilog packages.  
Each web service references `OpenTelemetry.Instrumentation.AspNetCore` locally.  
Each service's csproj references `SharedKernel` (already the case).

---

## Out of Scope

- Alerting rules (Grafana Alerting) — future
- Production-grade Tempo/Loki storage backends (S3, GCS) — future
- Sampling rules — default 100% trace sampling for dev; future: probabilistic for prod
- Service mesh / Envoy sidecar tracing — not needed at this scale
