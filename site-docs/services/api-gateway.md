# API Gateway

**Port:** 5010  
**Technology:** YARP 2.3 (Microsoft reverse proxy), RedisRateLimiting.AspNetCore, StackExchange.Redis

The API Gateway is the single entry point for all SPA API traffic. It handles CORS, enforces Redis-backed rate limiting, and routes requests to the appropriate backend service via YARP.

## Route table

| Incoming path | Cluster | Upstream path |
|---|---|---|
| `/api/cards/{**rest}` | card-catalog | `/cards/{**rest}` |
| `/api/portfolio/{**rest}` | portfolio | `/portfolio/{**rest}` |
| `/api/listings/{**rest}` | marketplace | `/listings/{**rest}` |
| `/api/notifications/{**rest}` | notification | `/notifications/{**rest}` |
| `/api/hubs/{**rest}` | notification | `/hubs/{**rest}` (WebSocket) |
| `/api/prices/{**rest}` | pricing | `/prices/{**rest}` |

The path prefix `/api/` is stripped before forwarding. YARP handles WebSocket protocol upgrade transparently for the SignalR hub route.

## Rate limiting

Rate limiting is implemented using `.NET 8`'s built-in `AddRateLimiter` with a `PartitionedRateLimiter<HttpContext>` backed by Redis for distributed counter storage.

| Client type | Partition key | Limit |
|---|---|---|
| Anonymous | `rl:anon:<X-Forwarded-For or RemoteIpAddress>` | 60 req / 60 sec |
| Authenticated | `rl:auth:<JWT sub claim>` | 300 req / 60 sec |

The JWT `sub` claim is extracted by base64url-decoding the token payload — no signature validation is performed at the gateway level in v1. This is intentional: the gateway is a routing layer, and full token validation adds latency; the actual services validate tokens via the IdentityServer JWKS endpoint.

When a limit is exceeded the gateway returns `429 Too Many Requests` with `Retry-After` header.

```json
// 429 response body
{ "error": "Rate limit exceeded. Try again in 60 seconds." }
```

## CORS

The gateway allows the frontend origin (configurable via `Cors:AllowedOrigins`):

```json
"Cors": {
  "AllowedOrigins": "http://localhost,http://localhost:3000"
}
```

`AllowAnyHeader`, `AllowAnyMethod`, and `AllowCredentials` are enabled — the SPA sends cookies for the OIDC session.

## Health check

```
GET http://localhost:5010/health → 200 OK
```

## Observability

The gateway emits:
- **Traces** — every proxied request gets a span with the upstream cluster as a tag; visible in Grafana Tempo
- **Metrics** — request rate, latency, and error rate per route exported to Prometheus; visible in the Grafana "Services Overview" dashboard
- **Logs** — structured JSON to Loki via Serilog, enriched with the trace ID

## Configuration

Rate limits and Redis connection are configurable without redeployment:

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

In tests, `RateLimitConfig` is overridden via `WebApplicationFactory.ConfigureAppConfiguration` to set limits of 1–5 requests so test scenarios don't require many calls.
