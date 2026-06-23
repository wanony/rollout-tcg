# Rate Limiting — Design Spec

**Date:** 2026-06-23
**Status:** Approved
**Scope:** Redis-backed sliding window rate limiting on ApiGateway; two tiers (anonymous by IP, authenticated by user ID)

---

## Goal

Protect all ApiGateway routes from abuse. Anonymous callers limited by client IP; authenticated callers (valid JWT `sub` claim) get a higher per-user limit. All counter state stored in Redis — survives restarts, works across replicas.

---

## Architecture

Rate limiting lives entirely inside the ApiGateway service. No other service is modified. The middleware sits at the top of the ASP.NET pipeline — before routing, before auth middleware.

```
Client → ApiGateway → [RateLimitMiddleware] → [Routes]
                             ↓
                           Redis
```

Policy selection logic (single `PartitionedRateLimiter<HttpContext>`):
- Request has a `sub` JWT claim → **authenticated** policy (300 req/min, keyed by sub)
- No `sub` claim → **anonymous** policy (60 req/min, keyed by remote IP)

On rejection: HTTP 429 + `Retry-After: <seconds>` response header. No response body required.

---

## Library

**`RedisRateLimiting.AspNetCore`** (NuGet by cristipufu) — extends the .NET 8 built-in `Microsoft.AspNetCore.RateLimiting` API with Redis-backed algorithms. Uses Lua scripts for atomic sliding window counter operations (INCR + EXPIRE equivalent). Zero race conditions.

**`StackExchange.Redis`** — Redis connection multiplexer. Registered as `IConnectionMultiplexer` singleton in DI.

---

## Files

### New

```
src/ApiGateway/Infrastructure/RateLimiting/RateLimitingExtensions.cs
tests/ApiGateway.Tests/TCGTrading.ApiGateway.Tests.csproj
tests/ApiGateway.Tests/Infrastructure/RateLimiting/RateLimitingIntegrationTests.cs
```

### Modified

```
src/ApiGateway/Program.cs              — AddRateLimiting() + UseRateLimiter()
src/ApiGateway/appsettings.json        — RateLimit + Redis config sections
src/ApiGateway/TCGTrading.ApiGateway.csproj  — add Redis.RateLimiting, StackExchange.Redis
docker-compose.yml                     — add redis service + ApiGateway depends_on
Directory.Packages.props               — add Redis.RateLimiting, StackExchange.Redis, Testcontainers.Redis versions
```

---

## Configuration

`src/ApiGateway/appsettings.json` additions:

```json
"RateLimit": {
  "AnonymousPermitLimit": 60,
  "AuthenticatedPermitLimit": 300,
  "WindowSeconds": 60
},
"Redis": {
  "ConnectionString": "redis:6379"
}
```

`src/ApiGateway/appsettings.Development.json` additions:

```json
"Redis": {
  "ConnectionString": "localhost:6379"
}
```

Configuration read via `IConfiguration` in `RateLimitingExtensions.cs`. No hardcoded values anywhere.

---

## `RateLimitingExtensions.cs`

```csharp
namespace TCGTrading.ApiGateway.Infrastructure.RateLimiting;

public static class RateLimitingExtensions
{
    public static IHostApplicationBuilder AddRateLimiting(
        this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration["Redis:ConnectionString"]
            ?? "localhost:6379";
        var permitAnonymous = builder.Configuration.GetValue<int>("RateLimit:AnonymousPermitLimit", 60);
        var permitAuthenticated = builder.Configuration.GetValue<int>("RateLimit:AuthenticatedPermitLimit", 300);
        var windowSeconds = builder.Configuration.GetValue<int>("RateLimit:WindowSeconds", 60);

        var redis = ConnectionMultiplexer.Connect(connectionString);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(
                        MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync(
                    "Rate limit exceeded.", cancellationToken);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var sub = httpContext.User.FindFirst("sub")?.Value;

                if (!string.IsNullOrEmpty(sub))
                {
                    return RateLimitPartition.GetRedisSlidingWindowLimiter(
                        partitionKey: $"rl:auth:{sub}",
                        factory: _ => new RedisSlidingWindowRateLimiterOptions
                        {
                            ConnectionMultiplexerFactory = () =>
                                httpContext.RequestServices
                                    .GetRequiredService<IConnectionMultiplexer>(),
                            PermitLimit = permitAuthenticated,
                            Window = TimeSpan.FromSeconds(windowSeconds),
                        });
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetRedisSlidingWindowLimiter(
                    partitionKey: $"rl:anon:{ip}",
                    factory: _ => new RedisSlidingWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () =>
                            httpContext.RequestServices
                                .GetRequiredService<IConnectionMultiplexer>(),
                        PermitLimit = permitAnonymous,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                    });
            });
        });

        return builder;
    }
}
```

**Namespace:** `TCGTrading.ApiGateway.Infrastructure.RateLimiting`

**No imports from Domain or Application layers** — clean arch preserved.

---

## `Program.cs` changes

```csharp
builder.AddRateLimiting();     // after AddTelemetry

// pipeline order:
app.UseRateLimiter();          // before app.MapGet / route handlers
```

---

## Docker Compose

```yaml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
  volumes:
    - redis_data:/data

# add to volumes block:
redis_data:
```

ApiGateway service:
```yaml
api-gateway:
  depends_on:
    - redis
```

---

## Tests

New project `tests/ApiGateway.Tests/` (Microsoft.NET.Sdk.Web — needs `WebApplicationFactory`).

Three integration tests using `WebApplicationFactory<Program>` + Testcontainers Redis. The test factory overrides `Redis:ConnectionString` to point at the Testcontainers Redis instance.

### Test 1 — Anonymous limit enforced
Send 61 GET `/` requests from same IP. First 60 return 200. 61st returns 429 + `Retry-After` header present.

### Test 2 — Authenticated limit enforced
Send 301 GET `/` requests with identical `Authorization: Bearer <jwt with sub=user-1>`. First 300 return 200. 301st returns 429 + `Retry-After` header present.

### Test 3 — Authenticated caller bypasses IP limit
Send 61 GET `/` requests with valid JWT `sub` claim. All return 200 (authenticated limit is 300, not 60). Confirms policy selection logic works.

**JWT in tests:** Minimal hand-crafted JWT with `sub` claim (no signature validation in tests — ApiGateway stub does not validate tokens, only reads claims). Use `System.IdentityModel.Tokens.Jwt` or manual base64 construction.

---

## NuGet Versions

Add to `Directory.Packages.props`:

```xml
<PackageVersion Include="RedisRateLimiting.AspNetCore" Version="1.2.1" />
<PackageVersion Include="StackExchange.Redis" Version="2.8.16" />
<PackageVersion Include="Testcontainers.Redis" Version="3.10.0" />
```

---

## Clean Architecture Compliance

| Layer | Rate limiting code? |
|-------|-------------------|
| Domain | None |
| Application | None |
| Infrastructure (ApiGateway) | `RateLimitingExtensions.cs` — registers Redis + rate limiter |
| Presentation (ApiGateway Program.cs) | `UseRateLimiter()` call only |

`RateLimitingExtensions.cs` imports: `Microsoft.AspNetCore.*`, `StackExchange.Redis`, `Redis.RateLimiting`, `IConfiguration`. Zero SharedKernel Domain/Application imports.

---

## Out of Scope

- Per-endpoint rate limit overrides (all routes share the global limiter)
- Rate limit headers on successful responses (`X-RateLimit-Remaining`) — 429 + `Retry-After` only
- JWT signature validation in ApiGateway (stub service — IdentityService handles auth)
- Redis cluster / Sentinel configuration (single node; K8s plan adds HA)
- Rate limit bypass for health/metrics endpoints (`/metrics`, `/health`) — deferred; add `EnableRateLimiting(false)` attribute when those endpoints are added
