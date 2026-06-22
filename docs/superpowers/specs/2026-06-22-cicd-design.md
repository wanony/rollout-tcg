# CI/CD Pipeline — Design Spec

**Date:** 2026-06-22  
**Status:** Approved  
**Scope:** GitHub Actions CI (build + test) and Docker image build + push to ghcr.io for all 7 services

---

## Goal

Add a production-grade GitHub Actions pipeline: every branch push runs build + tests; every merge to `main` builds and pushes Docker images to GitHub Container Registry. Path filtering ensures only changed services rebuild.

---

## File Layout

```
.github/
  workflows/
    ci.yml              — build + test (all branches + PRs)
    docker.yml          — image build + push (main only)
  dependabot.yml        — weekly NuGet + Actions version updates
src/
  ApiGateway/Dockerfile
  CardCatalog/Dockerfile
  IdentityService/Dockerfile   (fix: add SharedKernel copy)
  Marketplace/Dockerfile
  Notification/Dockerfile
  Portfolio/Dockerfile
  Pricing/Dockerfile
```

---

## Workflow: `ci.yml`

**Triggers:**
```yaml
on:
  push:
    branches: ["**"]
  pull_request:
    branches: [main]
```

**Jobs:** Single `build-and-test` job on `ubuntu-latest`.

**Steps:**
1. `actions/checkout@v4`
2. `actions/setup-dotnet@v4` — version `8.0.x`
3. `actions/cache@v4` — NuGet packages, key `${{ runner.os }}-nuget-${{ hashFiles('**/Directory.Packages.props') }}`; path `~/.nuget/packages`
4. `dotnet restore TCGTrading.sln`
5. `dotnet build TCGTrading.sln --no-restore -c Release` — `TreatWarningsAsErrors=true` in `Directory.Build.props` enforces zero-warning builds; build failure if any warning exists
6. `dotnet test TCGTrading.sln --no-build -c Release --logger "github;annotations=true"` — runs SharedKernel.Tests (25), Pricing.Tests (3), IdentityService.Tests (7 — Testcontainers, requires Docker). `ubuntu-latest` runners have Docker available; Testcontainers pulls Postgres/RabbitMQ images automatically.

---

## Workflow: `docker.yml`

**Triggers:**
```yaml
on:
  push:
    branches: [main]
```

**Permissions:**
```yaml
permissions:
  contents: read
  packages: write
```

Required for `GITHUB_TOKEN` to push to `ghcr.io`.

### Job 1: `changes`

Uses `dorny/paths-filter@v3` to detect which services changed.

Filters:
| Filter key | Paths watched |
|------------|---------------|
| `shared` | `src/SharedKernel/**`, `Directory.Build.props`, `Directory.Packages.props` |
| `api-gateway` | `src/ApiGateway/**` |
| `card-catalog` | `src/CardCatalog/**` |
| `identity-service` | `src/IdentityService/**` |
| `marketplace` | `src/Marketplace/**` |
| `notification` | `src/Notification/**` |
| `portfolio` | `src/Portfolio/**` |
| `pricing` | `src/Pricing/**` |

If `shared=true`, all 7 service build jobs run regardless of their own filter output.

### Job 2: `build` (matrix)

`needs: [changes]`

Matrix:
```yaml
strategy:
  matrix:
    service:
      - name: api-gateway
        project: ApiGateway
        dll: TCGTrading.ApiGateway.dll
      - name: card-catalog
        project: CardCatalog
        dll: TCGTrading.CardCatalog.Api.dll
      - name: identity-service
        project: IdentityService
        dll: TCGTrading.IdentityService.dll
      - name: marketplace
        project: Marketplace
        dll: TCGTrading.Marketplace.Api.dll
      - name: notification
        project: Notification
        dll: TCGTrading.Notification.Api.dll
      - name: portfolio
        project: Portfolio
        dll: TCGTrading.Portfolio.Api.dll
      - name: pricing
        project: Pricing
        dll: TCGTrading.Pricing.Api.dll
```

Condition:
```yaml
if: |
  needs.changes.outputs.shared == 'true' ||
  needs.changes.outputs[matrix.service.name] == 'true'
```

Steps per matrix item:
1. `actions/checkout@v4`
2. `docker/login-action@v3` — registry `ghcr.io`, username `${{ github.actor }}`, password `${{ secrets.GITHUB_TOKEN }}`
3. `docker/metadata-action@v5` — generates OCI-compliant tags:
   - `ghcr.io/${{ github.repository_owner }}/tcgtrading-${{ matrix.service.name }}:sha-${{ github.sha }}`  
   - `ghcr.io/${{ github.repository_owner }}/tcgtrading-${{ matrix.service.name }}:main`  
   - `ghcr.io/${{ github.repository_owner }}/tcgtrading-${{ matrix.service.name }}:latest`
4. `docker/setup-buildx-action@v3`
5. `docker/build-push-action@v5`:
   - `context: .` (repo root — required for SharedKernel access)
   - `file: src/${{ matrix.service.project }}/Dockerfile`
   - `push: true`
   - `tags: ${{ steps.meta.outputs.tags }}`
   - `labels: ${{ steps.meta.outputs.labels }}`
   - `cache-from: type=gha`
   - `cache-to: type=gha,mode=max`

---

## Dockerfiles

All 7 services use the same multi-stage pattern. Key fix: SharedKernel copied before service source to preserve Docker layer caching, since SharedKernel changes less frequently than service code.

### Standard pattern (6 stub services)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/{Service}/TCGTrading.{Service}.csproj src/{Service}/

RUN dotnet restore src/{Service}/TCGTrading.{Service}.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/{Service}/ src/{Service}/

RUN dotnet publish src/{Service}/TCGTrading.{Service}.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.{Service}.dll"]
```

### IdentityService (keeps `curl` for healthcheck)

Same pattern as above plus:
```dockerfile
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
```

---

## `dependabot.yml`

```yaml
version: 2
updates:
  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: weekly

  - package-ecosystem: nuget
    directory: /
    schedule:
      interval: weekly
```

Keeps Actions pinned to latest patch versions and flags stale NuGet packages automatically.

---

## Layer Caching Strategy

Docker layer ordering in Dockerfiles is deliberate:

```
[Directory.Build.props + Directory.Packages.props]  — changes rarely → cached long
[SharedKernel.csproj]                               — changes infrequently
[{Service}.csproj]                                  — changes on dep additions
[dotnet restore]                                    — cached if above unchanged
[SharedKernel source]                               — changes less than service
[{Service} source]                                  — changes most often
[dotnet publish]
```

This ordering maximises cache hits on repeated pushes.

---

## Out of Scope

- Deploy step — deferred to Kubernetes/Helm plan
- Code signing / SBOM attestation — future
- Security scanning (Trivy/grype) on images — future
- Branch protection rules (require CI to pass) — configure manually in GitHub repo settings; cannot be automated via Actions
