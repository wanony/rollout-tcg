# Observability Stack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add production-grade observability to Rollout TCG — distributed traces, metrics, and structured logs visible in a single Grafana UI, activated via `docker compose --profile observability up`.

**Architecture:** OpenTelemetry SDK in all 7 services exports via OTLP to a collector, which fans out to Grafana Tempo (traces), Prometheus (metrics), and Loki (logs). A shared `AddTelemetry(serviceName)` extension in SharedKernel configures Serilog + OTel core; each web service adds ASP.NET Core instrumentation and Prometheus scrape endpoint locally.

**Tech Stack:** OpenTelemetry .NET 1.9.x, Serilog 8.x, Grafana Tempo, Prometheus, Grafana Loki, Grafana 10.x, otel-collector-contrib.

## Global Constraints

- Target framework: `net8.0` throughout
- Central package versions in `Directory.Packages.props` — never set versions in individual csproj files
- Clean architecture strictly: Domain and Application layers have zero OTel imports; all telemetry code lives in Infrastructure
- All 7 services use `WebApplicationBuilder` — they all get ASP.NET Core instrumentation
- `TelemetryExtensions.cs` lives in `src/SharedKernel/Infrastructure/Telemetry/` — Infrastructure layer
- Manual span interfaces live in `Application/Interfaces/` of the owning service — no OTel imports there
- Manual span implementations live in `Infrastructure/Telemetry/` of the owning service
- Observability containers use `profiles: [observability]` in docker-compose — no impact on `docker compose up` without the flag
- `appsettings.json` uses container hostnames (e.g. `http://otel-collector:4317`); `appsettings.Development.json` overrides to `http://localhost:4317` for local `dotnet run`

---

## File Map

**New files:**
```
infra/otel-collector/config.yaml
infra/tempo/tempo.yaml
infra/prometheus/prometheus.yml
infra/loki/loki.yaml
infra/grafana/provisioning/datasources/datasources.yaml
infra/grafana/provisioning/dashboards/dashboards.yaml
infra/grafana/dashboards/services-overview.json
infra/grafana/dashboards/infrastructure.json
infra/grafana/dashboards/traces.json
src/SharedKernel/Infrastructure/Telemetry/TelemetryExtensions.cs
tests/SharedKernel.Tests/Infrastructure/Telemetry/TelemetryExtensionsTests.cs
src/Pricing/Application/Interfaces/IPricingTelemetry.cs
src/Pricing/Infrastructure/Telemetry/PricingTelemetry.cs
tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj
tests/Pricing.Tests/Infrastructure/Telemetry/PricingTelemetryTests.cs
```

**Modified files:**
```
Directory.Packages.props                         — add OTel + Serilog package versions
docker-compose.yml                               — add 5 observability containers + volumes
src/SharedKernel/TCGTrading.SharedKernel.csproj  — add OTel + Serilog refs
src/ApiGateway/TCGTrading.ApiGateway.csproj      — add OTel ASP.NET + Prometheus refs
src/ApiGateway/Program.cs                        — call AddTelemetry
src/ApiGateway/appsettings.json                  — add Telemetry section
src/ApiGateway/appsettings.Development.json      — add Telemetry section (localhost)
src/CardCatalog/TCGTrading.CardCatalog.Api.csproj
src/CardCatalog/Program.cs
src/CardCatalog/appsettings.json
src/CardCatalog/appsettings.Development.json
src/IdentityService/TCGTrading.IdentityService.csproj
src/IdentityService/Program.cs
src/IdentityService/appsettings.json
src/IdentityService/appsettings.Development.json
src/Marketplace/TCGTrading.Marketplace.Api.csproj
src/Marketplace/Program.cs
src/Marketplace/appsettings.json
src/Marketplace/appsettings.Development.json
src/Notification/TCGTrading.Notification.Api.csproj
src/Notification/Program.cs
src/Notification/appsettings.json
src/Notification/appsettings.Development.json
src/Portfolio/TCGTrading.Portfolio.Api.csproj
src/Portfolio/Program.cs
src/Portfolio/appsettings.json
src/Portfolio/appsettings.Development.json
src/Pricing/TCGTrading.Pricing.Api.csproj
src/Pricing/Program.cs
src/Pricing/appsettings.json
src/Pricing/appsettings.Development.json
TCGTrading.sln                                   — add Pricing.Tests project
```

---

### Task 1: Add NuGet packages to central version management

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/SharedKernel/TCGTrading.SharedKernel.csproj`

**Interfaces:**
- Produces: Package versions available for all csproj files to reference without specifying versions

- [ ] **Step 1: Add package versions to `Directory.Packages.props`**

Replace the closing `</ItemGroup>` and `</Project>` with the following additions, then close them:

```xml
    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.9.0-rc.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />
    <!-- Serilog -->
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.2" />
    <PackageVersion Include="Serilog.Enrichers.Span" Version="3.1.0" />
    <PackageVersion Include="Serilog.Sinks.Grafana.Loki" Version="8.3.0" />
    <PackageVersion Include="Serilog.Formatting.Compact" Version="2.0.0" />
    <!-- Test infrastructure -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

- [ ] **Step 2: Add package references to SharedKernel**

Full replacement content for `src/SharedKernel/TCGTrading.SharedKernel.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="MassTransit" />
    <PackageReference Include="MediatR" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Enrichers.Span" />
    <PackageReference Include="Serilog.Sinks.Grafana.Loki" />
    <PackageReference Include="Serilog.Formatting.Compact" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/SharedKernel/TCGTrading.SharedKernel.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props src/SharedKernel/TCGTrading.SharedKernel.csproj
git commit -m "feat(observability): add OTel and Serilog NuGet packages"
```

---

### Task 2: Infrastructure config files

**Files:**
- Create: `infra/otel-collector/config.yaml`
- Create: `infra/tempo/tempo.yaml`
- Create: `infra/prometheus/prometheus.yml`
- Create: `infra/loki/loki.yaml`

**Interfaces:**
- Consumes: Nothing (static config files)
- Produces: Config files mounted into docker containers in Task 3

- [ ] **Step 1: Create otel-collector config**

Create `infra/otel-collector/config.yaml`:

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 5s
    send_batch_size: 1024
  memory_limiter:
    check_interval: 1s
    limit_mib: 512

exporters:
  otlp/tempo:
    endpoint: tempo:4317
    tls:
      insecure: true
  prometheusremotewrite:
    endpoint: http://prometheus:9090/api/v1/write
    tls:
      insecure_skip_verify: true
  loki:
    endpoint: http://loki:3100/loki/api/v1/push

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlp/tempo]
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [prometheusremotewrite]
    logs:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [loki]
```

- [ ] **Step 2: Create Tempo config**

Create `infra/tempo/tempo.yaml`:

```yaml
server:
  http_listen_port: 3200

distributor:
  receivers:
    otlp:
      protocols:
        grpc:
          endpoint: 0.0.0.0:4317

ingester:
  trace_idle_period: 10s
  max_block_bytes: 1_000_000
  max_block_duration: 5m

compactor:
  compaction:
    compaction_window: 1h

storage:
  trace:
    backend: local
    local:
      path: /tmp/tempo/blocks
    wal:
      path: /tmp/tempo/wal

query_frontend:
  search:
    duration_slo: 5s
    throughput_bytes_slo: 1.073741824e+09
```

- [ ] **Step 3: Create Prometheus config**

Create `infra/prometheus/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

storage:
  tsdb:
    out_of_order_time_window: 10m

scrape_configs:
  - job_name: tcgtrading-services
    static_configs:
      - targets:
          - host.docker.internal:5000
          - host.docker.internal:5001
          - host.docker.internal:5002
          - host.docker.internal:5003
          - host.docker.internal:5004
          - host.docker.internal:5005
          - host.docker.internal:5006
        labels:
          app: tcgtrading
    metrics_path: /metrics
```

Note: `host.docker.internal` lets Prometheus (running in Docker) scrape services running on the host machine via `dotnet run`. When services are containerised later, replace with container names.

- [ ] **Step 4: Create Loki config**

Create `infra/loki/loki.yaml`:

```yaml
auth_enabled: false

server:
  http_listen_port: 3100
  grpc_listen_port: 9096

common:
  instance_addr: 127.0.0.1
  path_prefix: /tmp/loki
  storage:
    filesystem:
      chunks_directory: /tmp/loki/chunks
      rules_directory: /tmp/loki/rules
  replication_factor: 1
  ring:
    kvstore:
      store: inmemory

query_range:
  results_cache:
    cache:
      embedded_cache:
        enabled: true
        max_size_mb: 100

schema_config:
  configs:
    - from: 2020-10-24
      store: tsdb
      object_store: filesystem
      schema: v13
      index:
        prefix: index_
        period: 24h

limits_config:
  reject_old_samples: true
  reject_old_samples_max_age: 168h
  allow_structured_metadata: true
```

- [ ] **Step 5: Commit**

```bash
git add infra/
git commit -m "feat(observability): add otel-collector, tempo, prometheus, loki configs"
```

---

### Task 3: docker-compose observability profile

**Files:**
- Modify: `docker-compose.yml`

**Interfaces:**
- Consumes: Config files from Task 2 (mounted as volumes)
- Produces: `docker compose --profile observability up` starts all 5 observability containers

- [ ] **Step 1: Add observability services and volumes to `docker-compose.yml`**

After the existing `identity-service` service block and before the `volumes:` section, add:

```yaml
  otel-collector:
    image: otel/opentelemetry-collector-contrib:0.104.0
    profiles: [observability]
    volumes:
      - ./infra/otel-collector/config.yaml:/etc/otel-collector-config.yaml
    command: ["--config=/etc/otel-collector-config.yaml"]
    ports:
      - "4317:4317"
      - "4318:4318"
    depends_on:
      - tempo
      - prometheus
      - loki

  tempo:
    image: grafana/tempo:2.5.0
    profiles: [observability]
    volumes:
      - ./infra/tempo/tempo.yaml:/etc/tempo.yaml
      - tempo_data:/tmp/tempo
    command: ["-config.file=/etc/tempo.yaml"]
    ports:
      - "3200:3200"
      - "4319:4317"

  prometheus:
    image: prom/prometheus:v2.53.0
    profiles: [observability]
    volumes:
      - ./infra/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    command:
      - --config.file=/etc/prometheus/prometheus.yml
      - --storage.tsdb.path=/prometheus
      - --web.enable-remote-write-receiver
    ports:
      - "9090:9090"
    extra_hosts:
      - "host.docker.internal:host-gateway"

  loki:
    image: grafana/loki:3.1.0
    profiles: [observability]
    volumes:
      - ./infra/loki/loki.yaml:/etc/loki/loki.yaml
      - loki_data:/tmp/loki
    command: ["-config.file=/etc/loki/loki.yaml"]
    ports:
      - "3100:3100"

  grafana:
    image: grafana/grafana:11.1.0
    profiles: [observability]
    environment:
      GF_AUTH_ANONYMOUS_ENABLED: "true"
      GF_AUTH_ANONYMOUS_ORG_ROLE: Admin
      GF_AUTH_DISABLE_LOGIN_FORM: "true"
    volumes:
      - ./infra/grafana/provisioning:/etc/grafana/provisioning
      - ./infra/grafana/dashboards:/etc/grafana/dashboards
      - grafana_data:/var/lib/grafana
    ports:
      - "3000:3000"
    depends_on:
      - tempo
      - prometheus
      - loki
```

Under the existing `volumes:` section, add:

```yaml
  tempo_data:
  prometheus_data:
  loki_data:
  grafana_data:
```

- [ ] **Step 2: Validate compose file**

```bash
docker compose --profile observability config
```

Expected: Full resolved YAML printed with no errors.

- [ ] **Step 3: Commit**

```bash
git add docker-compose.yml
git commit -m "feat(observability): add observability containers to docker-compose profile"
```

---

### Task 4: Grafana provisioning and dashboards

**Files:**
- Create: `infra/grafana/provisioning/datasources/datasources.yaml`
- Create: `infra/grafana/provisioning/dashboards/dashboards.yaml`
- Create: `infra/grafana/dashboards/services-overview.json`
- Create: `infra/grafana/dashboards/infrastructure.json`
- Create: `infra/grafana/dashboards/traces.json`

**Interfaces:**
- Consumes: Nothing
- Produces: Grafana auto-loads datasources and dashboards on startup

- [ ] **Step 1: Create datasources provisioning**

Create `infra/grafana/provisioning/datasources/datasources.yaml`:

```yaml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    uid: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    jsonData:
      timeInterval: 15s

  - name: Tempo
    type: tempo
    uid: tempo
    access: proxy
    url: http://tempo:3200
    jsonData:
      tracesToLogsV2:
        datasourceUid: loki
        filterByTraceID: true
        filterBySpanID: false
      lokiSearch:
        datasourceUid: loki
      serviceMap:
        datasourceUid: prometheus

  - name: Loki
    type: loki
    uid: loki
    access: proxy
    url: http://loki:3100
    jsonData:
      derivedFields:
        - name: TraceID
          matcherRegex: '"TraceId":"(\w+)"'
          url: '$${__value.raw}'
          datasourceUid: tempo
```

- [ ] **Step 2: Create dashboards provisioning**

Create `infra/grafana/provisioning/dashboards/dashboards.yaml`:

```yaml
apiVersion: 1

providers:
  - name: TCGTrading
    orgId: 1
    folder: TCGTrading
    type: file
    disableDeletion: false
    updateIntervalSeconds: 30
    allowUiUpdates: true
    options:
      path: /etc/grafana/dashboards
```

- [ ] **Step 3: Create services overview dashboard**

Create `infra/grafana/dashboards/services-overview.json`:

```json
{
  "title": "TCGTrading - Services Overview",
  "uid": "tcg-services-overview",
  "schemaVersion": 38,
  "version": 1,
  "refresh": "10s",
  "panels": [
    {
      "id": 1,
      "title": "HTTP Request Rate (req/s)",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 12, "x": 0, "y": 0 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "sum(rate(http_server_request_duration_seconds_count[5m])) by (service_name)",
          "legendFormat": "{{service_name}}"
        }
      ]
    },
    {
      "id": 2,
      "title": "HTTP Error Rate (5xx req/s)",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 12, "x": 12, "y": 0 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~\"5..\"}[5m])) by (service_name)",
          "legendFormat": "{{service_name}}"
        }
      ]
    },
    {
      "id": 3,
      "title": "Latency p50 (s)",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 8, "x": 0, "y": 8 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "histogram_quantile(0.50, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le, service_name))",
          "legendFormat": "{{service_name}} p50"
        }
      ]
    },
    {
      "id": 4,
      "title": "Latency p95 (s)",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 8, "x": 8, "y": 8 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le, service_name))",
          "legendFormat": "{{service_name}} p95"
        }
      ]
    },
    {
      "id": 5,
      "title": "Latency p99 (s)",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 8, "x": 16, "y": 8 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "histogram_quantile(0.99, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le, service_name))",
          "legendFormat": "{{service_name}} p99"
        }
      ]
    }
  ]
}
```

- [ ] **Step 4: Create infrastructure dashboard**

Create `infra/grafana/dashboards/infrastructure.json`:

```json
{
  "title": "TCGTrading - Infrastructure",
  "uid": "tcg-infrastructure",
  "schemaVersion": 38,
  "version": 1,
  "refresh": "15s",
  "panels": [
    {
      "id": 1,
      "title": ".NET GC Collections/s",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 12, "x": 0, "y": 0 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "sum(rate(process_runtime_dotnet_gc_collections_count_total[5m])) by (generation, service_name)",
          "legendFormat": "{{service_name}} gen{{generation}}"
        }
      ]
    },
    {
      "id": 2,
      "title": "GC Heap Size (bytes)",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 12, "x": 12, "y": 0 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "process_runtime_dotnet_gc_heap_size_bytes",
          "legendFormat": "{{service_name}} {{generation}}"
        }
      ]
    },
    {
      "id": 3,
      "title": "Thread Pool Threads",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 12, "x": 0, "y": 8 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "process_runtime_dotnet_thread_pool_threads_count",
          "legendFormat": "{{service_name}}"
        }
      ]
    },
    {
      "id": 4,
      "title": "Active HTTP Requests",
      "type": "timeseries",
      "gridPos": { "h": 8, "w": 12, "x": 12, "y": 8 },
      "datasource": { "type": "prometheus", "uid": "prometheus" },
      "targets": [
        {
          "expr": "http_server_active_requests",
          "legendFormat": "{{service_name}}"
        }
      ]
    }
  ]
}
```

- [ ] **Step 5: Create traces dashboard**

Create `infra/grafana/dashboards/traces.json`:

```json
{
  "title": "TCGTrading - Distributed Traces",
  "uid": "tcg-traces",
  "schemaVersion": 38,
  "version": 1,
  "panels": [
    {
      "id": 1,
      "title": "Trace Explorer",
      "type": "traces",
      "gridPos": { "h": 20, "w": 24, "x": 0, "y": 0 },
      "datasource": { "type": "tempo", "uid": "tempo" },
      "targets": [
        {
          "queryType": "traceqlSearch",
          "filters": [
            {
              "id": "service-name",
              "scope": "resource",
              "tag": "service.name",
              "operator": "=",
              "valueType": "string"
            },
            {
              "id": "min-duration",
              "tag": "duration",
              "operator": ">",
              "valueType": "duration",
              "value": "0ms"
            }
          ]
        }
      ]
    }
  ]
}
```

- [ ] **Step 6: Commit**

```bash
git add infra/grafana/
git commit -m "feat(observability): add Grafana datasource and dashboard provisioning"
```

---

### Task 5: SharedKernel `AddTelemetry` extension + tests

**Files:**
- Create: `src/SharedKernel/Infrastructure/Telemetry/TelemetryExtensions.cs`
- Modify: `tests/SharedKernel.Tests/TCGTrading.SharedKernel.Tests.csproj`
- Create: `tests/SharedKernel.Tests/Infrastructure/Telemetry/TelemetryExtensionsTests.cs`

**Interfaces:**
- Produces: `AddTelemetry(this IHostApplicationBuilder builder, string serviceName)` — returns `IHostApplicationBuilder` for fluent chaining; configures Serilog + OTLP trace exporter (EF Core, HttpClient, MassTransit) + OTLP metrics exporter (HttpClient, Runtime)

- [ ] **Step 1: Write the failing test**

Add `Microsoft.Extensions.Hosting` to the test project. Full replacement for `tests/SharedKernel.Tests/TCGTrading.SharedKernel.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RunSettingsFilePath>$(MSBuildProjectDirectory)/test.runsettings</RunSettingsFilePath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="FluentAssertions" />
    <ProjectReference Include="../../src/SharedKernel/TCGTrading.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

Create `tests/SharedKernel.Tests/Infrastructure/Telemetry/TelemetryExtensionsTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

namespace TCGTrading.SharedKernel.Tests.Infrastructure.Telemetry;

public class TelemetryExtensionsTests
{
    [Fact]
    public void AddTelemetry_BuildsHostWithoutThrowing()
    {
        var builder = Host.CreateApplicationBuilder();

        var act = () =>
        {
            builder.AddTelemetry("test-service");
            using var host = builder.Build();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AddTelemetry_ReturnsBuilder_AllowingFluentChaining()
    {
        var builder = Host.CreateApplicationBuilder();

        var result = builder.AddTelemetry("test-service");

        result.Should().BeSameAs(builder);
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "FullyQualifiedName~TelemetryExtensionsTests" -v minimal
```

Expected: FAIL — `The type or namespace name 'TelemetryExtensions' does not exist`

- [ ] **Step 3: Implement `TelemetryExtensions.cs`**

Create `src/SharedKernel/Infrastructure/Telemetry/TelemetryExtensions.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

namespace TCGTrading.SharedKernel.Infrastructure.Telemetry;

public static class TelemetryExtensions
{
    public static IHostApplicationBuilder AddTelemetry(
        this IHostApplicationBuilder builder, string serviceName)
    {
        var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var lokiEndpoint = builder.Configuration["Telemetry:LokiEndpoint"] ?? "http://localhost:3100";

        builder.Services.AddSerilog((_, lc) => lc
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.GrafanaLoki(
                lokiEndpoint,
                labels: new[]
                {
                    new LokiLabel { Key = "service", Value = serviceName },
                    new LokiLabel { Key = "app", Value = "tcgtrading" }
                }));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: "1.0.0"))
            .WithTracing(t => t
                .AddSource(serviceName)
                .AddSource("MassTransit")
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(m => m
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return builder;
    }
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "FullyQualifiedName~TelemetryExtensionsTests" -v minimal
```

Expected: `Passed! - 2 passed`

- [ ] **Step 5: Run full SharedKernel test suite to check for regressions**

```bash
dotnet test tests/SharedKernel.Tests/ -v minimal
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/SharedKernel/Infrastructure/Telemetry/TelemetryExtensions.cs \
        tests/SharedKernel.Tests/TCGTrading.SharedKernel.Tests.csproj \
        tests/SharedKernel.Tests/Infrastructure/Telemetry/TelemetryExtensionsTests.cs
git commit -m "feat(observability): add AddTelemetry SharedKernel extension with Serilog + OTel"
```

---

### Task 6: Wire `AddTelemetry` into all 7 services

**Files:**
- Modify: `src/{Service}/TCGTrading.{Service}.csproj` (7 files)
- Modify: `src/{Service}/Program.cs` (7 files)
- Modify: `src/{Service}/appsettings.json` (7 files)
- Modify: `src/{Service}/appsettings.Development.json` (7 files)

**Interfaces:**
- Consumes: `AddTelemetry(serviceName)` from Task 5
- Produces: All services emit traces, metrics, logs on startup; `/metrics` endpoint on each

Apply the following pattern to **all 7 services**: ApiGateway, CardCatalog, IdentityService, Marketplace, Notification, Portfolio, Pricing.

Service name mapping:
| Service | serviceName | Port |
|---------|-------------|------|
| ApiGateway | `"api-gateway"` | 5000 |
| CardCatalog | `"card-catalog"` | 5002 |
| IdentityService | `"identity-service"` | 5001 (existing) |
| Marketplace | `"marketplace"` | 5003 |
| Notification | `"notification"` | 5004 |
| Portfolio | `"portfolio"` | 5005 |
| Pricing | `"pricing"` | 5006 |

- [ ] **Step 1: Update each service's csproj to add OTel ASP.NET Core + Prometheus packages**

For each of the 7 service csproj files, add these two package references:

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
```

Example — `src/ApiGateway/TCGTrading.ApiGateway.csproj` after change:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../SharedKernel/TCGTrading.SharedKernel.csproj" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
  </ItemGroup>
</Project>
```

Apply the same pattern to the remaining 6 services. IdentityService already has other packages — just add the two OTel lines to its existing `<ItemGroup>`.

- [ ] **Step 2: Update Program.cs for stub services (ApiGateway, CardCatalog, Marketplace, Notification, Portfolio)**

Replace each stub `Program.cs` with the wired version. Example for `src/ApiGateway/Program.cs`:

```csharp
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("api-gateway");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();
```

Apply the same pattern with the correct `serviceName` from the table above:
- `src/CardCatalog/Program.cs` → `"card-catalog"`
- `src/Marketplace/Program.cs` → `"marketplace"`
- `src/Notification/Program.cs` → `"notification"`
- `src/Portfolio/Program.cs` → `"portfolio"`

- [ ] **Step 3: Update IdentityService Program.cs**

IdentityService has existing setup. Add telemetry at the top, after `var builder = WebApplication.CreateBuilder(args);` and before the existing `builder.Services.AddDbContext`:

```csharp
builder.AddTelemetry("identity-service");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());
```

And after `var app = builder.Build();` add:

```csharp
app.MapPrometheusScrapingEndpoint();
```

- [ ] **Step 4: Update Pricing Program.cs (telemetry only — custom spans come in Task 7)**

`src/Pricing/Program.cs`:

```csharp
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("pricing");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();
```

- [ ] **Step 5: Add Telemetry config to appsettings.json for all 7 services**

Add this block to each service's `appsettings.json` (alongside the existing `Logging` key):

```json
"Telemetry": {
  "OtlpEndpoint": "http://otel-collector:4317",
  "LokiEndpoint": "http://loki:3100"
}
```

- [ ] **Step 6: Add Telemetry config to appsettings.Development.json for all 7 services**

Add this block to each service's `appsettings.Development.json` (localhost endpoints for `dotnet run`):

```json
"Telemetry": {
  "OtlpEndpoint": "http://localhost:4317",
  "LokiEndpoint": "http://localhost:3100"
}
```

- [ ] **Step 7: Verify solution builds**

```bash
dotnet build TCGTrading.sln
```

Expected: `Build succeeded. 0 Error(s)` across all projects.

- [ ] **Step 8: Commit**

```bash
git add src/
git commit -m "feat(observability): wire AddTelemetry into all 7 services"
```

---

### Task 7: Pricing manual span interface and implementation

**Files:**
- Create: `src/Pricing/Application/Interfaces/IPricingTelemetry.cs`
- Create: `src/Pricing/Infrastructure/Telemetry/PricingTelemetry.cs`
- Modify: `src/Pricing/Program.cs` (DI registration)
- Create: `tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj`
- Create: `tests/Pricing.Tests/Infrastructure/Telemetry/PricingTelemetryTests.cs`
- Modify: `TCGTrading.sln` (add new test project)

**Interfaces:**
- Produces: `IPricingTelemetry` injectable into any Application handler; `PricingTelemetry` impl emits spans under `ActivitySource` named `"tcgtrading.pricing"`

- [ ] **Step 1: Create the Application-layer interface (zero OTel dependency)**

Create `src/Pricing/Application/Interfaces/IPricingTelemetry.cs`:

```csharp
namespace TCGTrading.Pricing.Application.Interfaces;

public interface IPricingTelemetry
{
    IDisposable? StartPriceCalculation(string cardId, string source);
    IDisposable? StartMarketDataFetch(string provider);
}
```

No `using OpenTelemetry` — this file must not import any OTel namespace.

- [ ] **Step 2: Write the failing tests**

Create `tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="FluentAssertions" />
    <ProjectReference Include="../../src/Pricing/TCGTrading.Pricing.Api.csproj" />
  </ItemGroup>
</Project>
```

Create `tests/Pricing.Tests/Infrastructure/Telemetry/PricingTelemetryTests.cs`:

```csharp
using System.Diagnostics;
using FluentAssertions;
using TCGTrading.Pricing.Infrastructure.Telemetry;

namespace TCGTrading.Pricing.Tests.Infrastructure.Telemetry;

public class PricingTelemetryTests : IDisposable
{
    private readonly ActivityListener _listener;
    private Activity? _lastStarted;

    public PricingTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "tcgtrading.pricing",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStarted = a => _lastStarted = a
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void StartPriceCalculation_EmitsActivity_WithCardIdTag()
    {
        var telemetry = new PricingTelemetry();

        using var activity = telemetry.StartPriceCalculation("card-123", "market");

        _lastStarted.Should().NotBeNull();
        _lastStarted!.OperationName.Should().Be("pricing.calculate");
        _lastStarted.GetTagItem("card.id").Should().Be("card-123");
        _lastStarted.GetTagItem("pricing.source").Should().Be("market");
    }

    [Fact]
    public void StartMarketDataFetch_EmitsActivity_WithProviderTag()
    {
        var telemetry = new PricingTelemetry();

        using var activity = telemetry.StartMarketDataFetch("tcgplayer");

        _lastStarted.Should().NotBeNull();
        _lastStarted!.OperationName.Should().Be("pricing.market_data_fetch");
        _lastStarted.GetTagItem("market.provider").Should().Be("tcgplayer");
    }

    [Fact]
    public void StartPriceCalculation_ReturnsNull_WhenNoListenerRegistered()
    {
        _listener.Dispose();
        var telemetry = new PricingTelemetry();

        var result = telemetry.StartPriceCalculation("card-123", "market");

        result.Should().BeNull();
    }

    public void Dispose() => _listener.Dispose();
}
```

- [ ] **Step 3: Add Pricing.Tests to solution**

```bash
dotnet sln TCGTrading.sln add tests/Pricing.Tests/TCGTrading.Pricing.Tests.csproj
```

- [ ] **Step 4: Create `tests/Pricing.Tests/GlobalUsings.cs`**

```csharp
global using Xunit;
```

- [ ] **Step 5: Run tests to confirm they fail**

```bash
dotnet test tests/Pricing.Tests/ --filter "FullyQualifiedName~PricingTelemetryTests" -v minimal
```

Expected: FAIL — `Could not find type or namespace 'PricingTelemetry'`

- [ ] **Step 6: Implement `PricingTelemetry`**

Create `src/Pricing/Infrastructure/Telemetry/PricingTelemetry.cs`:

```csharp
using System.Diagnostics;
using TCGTrading.Pricing.Application.Interfaces;

namespace TCGTrading.Pricing.Infrastructure.Telemetry;

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

- [ ] **Step 7: Register `PricingTelemetry` in DI and add ActivitySource to OTel**

Update `src/Pricing/Program.cs` to register the telemetry impl and add the custom source:

```csharp
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Infrastructure.Telemetry;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("pricing");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddSource("tcgtrading.pricing"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddSingleton<IPricingTelemetry, PricingTelemetry>();

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();
```

- [ ] **Step 8: Run the tests to confirm they pass**

```bash
dotnet test tests/Pricing.Tests/ --filter "FullyQualifiedName~PricingTelemetryTests" -v minimal
```

Expected: `Passed! - 3 passed`

- [ ] **Step 9: Full solution build and all tests**

```bash
dotnet build TCGTrading.sln
dotnet test TCGTrading.sln -v minimal
```

Expected: Build succeeded, all tests pass.

- [ ] **Step 10: Commit**

```bash
git add src/Pricing/ tests/Pricing.Tests/ TCGTrading.sln
git commit -m "feat(observability): add Pricing manual span interface and ActivitySource implementation"
```

---

### Task 8: Smoke test — verify end-to-end signal flow

**Files:** None — verification only.

**Interfaces:**
- Consumes: All prior tasks complete

- [ ] **Step 1: Start the observability stack**

```bash
docker compose --profile observability up -d
```

Expected: 5 containers start (otel-collector, tempo, prometheus, loki, grafana). Wait ~15 seconds for all to be healthy.

- [ ] **Step 2: Run Pricing service locally**

```bash
dotnet run --project src/Pricing --urls http://localhost:5006
```

Leave running. Open a second terminal.

- [ ] **Step 3: Generate some traffic**

```bash
for i in {1..10}; do curl -s http://localhost:5006/ > /dev/null; done
curl http://localhost:5006/metrics
```

Expected from `/metrics`: Prometheus exposition format with lines like:
```
http_server_request_duration_seconds_bucket{...} 10
process_runtime_dotnet_gc_collections_count_total{...} 1
```

- [ ] **Step 4: Verify Grafana datasources are reachable**

Open `http://localhost:3000` in a browser (no login required — anonymous admin enabled).

Navigate to **Connections → Data sources**. Confirm:
- Prometheus — green "Data source is working"
- Tempo — green "Data source is working"
- Loki — green "Data source is working"

- [ ] **Step 5: Verify traces appear in Tempo**

In Grafana, navigate to **Explore → Tempo datasource**.
Select **Search** tab.
Set **Service Name** filter to `pricing`.
Click **Run query**.

Expected: At least one trace visible from the 10 requests in Step 3. Click a trace to expand spans — should show `GET /` as root span.

- [ ] **Step 6: Verify metrics appear in Prometheus**

In Grafana, navigate to **Explore → Prometheus datasource**.
Run query: `http_server_request_duration_seconds_count`

Expected: Series with label `service_name="pricing"` and count ≥ 10.

- [ ] **Step 7: Verify logs appear in Loki**

In Grafana, navigate to **Explore → Loki datasource**.
Run query: `{service="pricing"}`

Expected: JSON log lines with `TraceId` field populated (confirming Serilog span enrichment works).

- [ ] **Step 8: Open dashboards**

Navigate to **Dashboards → TCGTrading folder**.
Open **Services Overview** — panels should show data for `pricing`.
Open **Infrastructure** — GC and thread pool panels should show data.
Open **Distributed Traces** — trace search panel loads.

- [ ] **Step 9: Stop services and commit smoke test note**

```bash
docker compose --profile observability down
```

```bash
git add .
git commit -m "feat(observability): complete observability stack - smoke test passed"
```

---

## Summary

| Task | Deliverable | Test |
|------|-------------|------|
| 1 | NuGet packages registered | `dotnet build SharedKernel` |
| 2 | Infra YAML configs | Visual review |
| 3 | docker-compose profile | `docker compose --profile observability config` |
| 4 | Grafana dashboards + provisioning | Visual — dashboards load on startup |
| 5 | `AddTelemetry` extension | 2 unit tests |
| 6 | All 7 services wired | `dotnet build TCGTrading.sln` |
| 7 | Pricing manual spans | 3 unit tests |
| 8 | End-to-end signal flow | Traces/metrics/logs visible in Grafana |
