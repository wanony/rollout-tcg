# Rate Limiting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Redis-backed sliding window rate limiting to ApiGateway — anonymous callers limited by IP (60/min), authenticated callers by JWT `sub` claim (300/min).

**Architecture:** A custom `RateLimitingExtensions` registers .NET 8's built-in `AddRateLimiter` backed by `RedisRateLimiting.AspNetCore`. The `GlobalLimiter` uses a `PartitionedRateLimiter` that reads `IOptions<RateLimitConfig>` and `IConnectionMultiplexer` from DI at request time — enabling test overrides via the `WebApplicationFactory` pattern. JWT `sub` extraction is done by manually parsing the Bearer token payload (base64url decode only — no signature validation in the stub gateway).

**Tech Stack:** `RedisRateLimiting.AspNetCore` 1.2.1, `StackExchange.Redis` 2.8.0, `Testcontainers.Redis` 3.10.0, `Microsoft.AspNetCore.Mvc.Testing` 10.0.0, `IOptions<T>`, `PartitionedRateLimiter<HttpContext>`

## Global Constraints

- Clean architecture: zero Domain or Application layer imports in `RateLimitingExtensions.cs` — only `Microsoft.AspNetCore.*`, `StackExchange.Redis`, `RedisRateLimiting.AspNetCore`, `Microsoft.Extensions.*`
- Redis service name in docker-compose: `redis` (already exists, port 6379) — no docker-compose changes needed
- Rate limit config keys: `RateLimit:AnonymousPermitLimit`, `RateLimit:AuthenticatedPermitLimit`, `RateLimit:WindowSeconds`
- Redis connection config key: `Redis:ConnectionString`
- Anonymous partition key prefix: `rl:anon:` + IP (reads `X-Forwarded-For` header first, falls back to `RemoteIpAddress`)
- Authenticated partition key prefix: `rl:auth:` + JWT `sub` claim value
- Test project target framework: `net10.0` — matches all existing test projects (IdentityService.Tests, Pricing.Tests, SharedKernel.Tests)
- All test projects use `NuGetAuditMode>direct</NuGetAuditMode>` — only declare packages you import directly
- `TreatWarningsAsErrors=true` in `Directory.Build.props` — zero warnings allowed
- Production defaults: `AnonymousPermitLimit=60`, `AuthenticatedPermitLimit=300`, `WindowSeconds=60`
- Test overrides (via `IClassFixture`): `AnonymousPermitLimit=3`, `AuthenticatedPermitLimit=5`, `WindowSeconds=60`
- Use unique `X-Forwarded-For` IPs and JWT `sub` values per test to avoid Redis key collisions across tests in the same fixture

---

## File Map

**New files:**
```
src/ApiGateway/Infrastructure/RateLimiting/RateLimitConfig.cs          — Task 2
src/ApiGateway/Infrastructure/RateLimiting/RateLimitingExtensions.cs    — Task 3
tests/ApiGateway.Tests/TCGTrading.ApiGateway.Tests.csproj
tests/ApiGateway.Tests/test.runsettings
tests/ApiGateway.Tests/GlobalUsings.cs
tests/ApiGateway.Tests/Fixtures/ApiGatewayFixture.cs
tests/ApiGateway.Tests/Infrastructure/RateLimiting/RateLimitingIntegrationTests.cs
```

**Modified files:**
```
Directory.Packages.props                           — add RedisRateLimiting.AspNetCore, StackExchange.Redis, Testcontainers.Redis
src/ApiGateway/TCGTrading.ApiGateway.csproj        — add package refs
src/ApiGateway/appsettings.json                    — add RateLimit + Redis config sections
src/ApiGateway/appsettings.Development.json        — add Redis dev connection string
src/ApiGateway/Program.cs                          — AddRateLimiting() + UseRateLimiter() + partial Program
TCGTrading.sln                                     — add ApiGateway.Tests project
```

---

### Task 1: Package setup and configuration

Add NuGet packages and wire configuration. No implementation yet — just foundation.

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/ApiGateway/TCGTrading.ApiGateway.csproj`
- Modify: `src/ApiGateway/appsettings.json`
- Modify: `src/ApiGateway/appsettings.Development.json`

**Interfaces:**
- Produces: `RedisRateLimiting.AspNetCore`, `StackExchange.Redis`, `Testcontainers.Redis` available as centrally managed versions; `RateLimit` and `Redis` config sections present in ApiGateway configuration

- [ ] **Step 1: Add package versions to `Directory.Packages.props`**

Open `Directory.Packages.props`. Add three new `PackageVersion` entries inside the `<ItemGroup>`:

```xml
<PackageVersion Include="RedisRateLimiting.AspNetCore" Version="1.2.1" />
<PackageVersion Include="StackExchange.Redis" Version="2.8.0" />
<PackageVersion Include="Testcontainers.Redis" Version="3.10.0" />
```

Add them after the existing `<!-- Test infrastructure -->` comment block, before the closing `</ItemGroup>`.

- [ ] **Step 2: Add package references to ApiGateway csproj**

Open `src/ApiGateway/TCGTrading.ApiGateway.csproj`. Add two new `PackageReference` entries inside the `<ItemGroup>`:

```xml
<PackageReference Include="RedisRateLimiting.AspNetCore" />
<PackageReference Include="StackExchange.Redis" />
```

The full file should look like:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../SharedKernel/TCGTrading.SharedKernel.csproj" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
    <PackageReference Include="RedisRateLimiting.AspNetCore" />
    <PackageReference Include="StackExchange.Redis" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add `RateLimit` and `Redis` config to `src/ApiGateway/appsettings.json`**

Current file content:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Telemetry": {
    "OtlpEndpoint": "http://otel-collector:4317",
    "LokiEndpoint": "http://loki:3100"
  }
}
```

Replace with:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Telemetry": {
    "OtlpEndpoint": "http://otel-collector:4317",
    "LokiEndpoint": "http://loki:3100"
  },
  "RateLimit": {
    "AnonymousPermitLimit": 60,
    "AuthenticatedPermitLimit": 300,
    "WindowSeconds": 60
  },
  "Redis": {
    "ConnectionString": "redis:6379"
  }
}
```

- [ ] **Step 4: Add `Redis` config to `src/ApiGateway/appsettings.Development.json`**

Current file content:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Telemetry": {
    "OtlpEndpoint": "http://localhost:4317",
    "LokiEndpoint": "http://localhost:3100"
  }
}
```

Replace with:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Telemetry": {
    "OtlpEndpoint": "http://localhost:4317",
    "LokiEndpoint": "http://localhost:3100"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

Note: `RateLimit` defaults are in `appsettings.json` — no override needed for Development (defaults are fine).

- [ ] **Step 5: Verify restore passes**

```bash
dotnet restore TCGTrading.sln
```

Expected: `Restore complete.` with zero errors.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/ApiGateway/TCGTrading.ApiGateway.csproj \
        src/ApiGateway/appsettings.json src/ApiGateway/appsettings.Development.json
git commit -m "feat(rate-limiting): add NuGet packages and config"
```

---

### Task 2: Test project and failing tests

Create `RateLimitConfig` POCO (needed by the test fixture), the `ApiGateway.Tests` project, and three failing integration tests. After this task, tests compile and run but all three fail at runtime because `AddRateLimiting()` has not been wired into `Program.cs` yet (no rate limiter = every request returns 200, not 429).

**Files:**
- Create: `src/ApiGateway/Infrastructure/RateLimiting/RateLimitConfig.cs`
- Create: `tests/ApiGateway.Tests/TCGTrading.ApiGateway.Tests.csproj`
- Create: `tests/ApiGateway.Tests/test.runsettings`
- Create: `tests/ApiGateway.Tests/GlobalUsings.cs`
- Create: `tests/ApiGateway.Tests/Fixtures/ApiGatewayFixture.cs`
- Create: `tests/ApiGateway.Tests/Infrastructure/RateLimiting/RateLimitingIntegrationTests.cs`
- Modify: `src/ApiGateway/Program.cs` — add `public partial class Program {}`
- Modify: `TCGTrading.sln` — add test project

**Interfaces:**
- Consumes: ApiGateway project (via ProjectReference), `RateLimitConfig` POCO (created in this task)
- Produces: 3 failing integration tests for Task 3 to make pass; `RateLimitConfig` class used by `ApiGatewayFixture` and `RateLimitingExtensions` (Task 3)

- [ ] **Step 1: Create `src/ApiGateway/Infrastructure/RateLimiting/RateLimitConfig.cs`**

This is a plain options POCO — no infrastructure imports, just property definitions.

```csharp
namespace TCGTrading.ApiGateway.Infrastructure.RateLimiting;

public sealed class RateLimitConfig
{
    public int AnonymousPermitLimit { get; set; } = 60;
    public int AuthenticatedPermitLimit { get; set; } = 300;
    public int WindowSeconds { get; set; } = 60;
}
```

- [ ] **Step 2: Create `tests/ApiGateway.Tests/TCGTrading.ApiGateway.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RunSettingsFilePath>$(MSBuildProjectDirectory)/test.runsettings</RunSettingsFilePath>
    <NuGetAuditMode>direct</NuGetAuditMode>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Testcontainers.Redis" />
    <PackageReference Include="StackExchange.Redis" />
    <ProjectReference Include="../../src/ApiGateway/TCGTrading.ApiGateway.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `tests/ApiGateway.Tests/test.runsettings`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <DOTNET_ROLL_FORWARD>LatestMajor</DOTNET_ROLL_FORWARD>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

- [ ] **Step 4: Create `tests/ApiGateway.Tests/GlobalUsings.cs`**

```csharp
global using Xunit;
```

- [ ] **Step 5: Add `public partial class Program {}` to `src/ApiGateway/Program.cs`**

Open `src/ApiGateway/Program.cs`. It currently ends with `app.Run();`. Append this line at the very end of the file (after `app.Run();`):

```csharp
// Exposes Program class to WebApplicationFactory<Program> in test projects
public partial class Program { }
```

The full file should look like:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
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

// Exposes Program class to WebApplicationFactory<Program> in test projects
public partial class Program { }
```

- [ ] **Step 6: Create `tests/ApiGateway.Tests/Fixtures/ApiGatewayFixture.cs`**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Redis;
using TCGTrading.ApiGateway.Infrastructure.RateLimiting;

namespace TCGTrading.ApiGateway.Tests;

public class ApiGatewayFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync() => await _redis.StartAsync();

    public new async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace production Redis with test container
            var redisDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null)
                services.Remove(redisDescriptor);

            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

            // Override rate limits to small values so tests don't send hundreds of requests
            services.Configure<RateLimitConfig>(cfg =>
            {
                cfg.AnonymousPermitLimit = 3;
                cfg.AuthenticatedPermitLimit = 5;
                cfg.WindowSeconds = 60;
            });
        });
    }
}
```

- [ ] **Step 7: Create `tests/ApiGateway.Tests/Infrastructure/RateLimiting/RateLimitingIntegrationTests.cs`**

```csharp
using System.Net;
using FluentAssertions;

namespace TCGTrading.ApiGateway.Tests.Infrastructure.RateLimiting;

public class RateLimitingIntegrationTests(ApiGatewayFixture fixture)
    : IClassFixture<ApiGatewayFixture>
{
    [Fact]
    public async Task AnonymousPolicy_EnforcesIpLimit_Returns429AfterLimit()
    {
        // Uses X-Forwarded-For: 10.1.1.1 — unique key so it doesn't collide with other tests
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.1");

        // First 3 requests should succeed (limit = 3 in test fixture)
        for (var i = 0; i < 3; i++)
        {
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"request {i + 1} of 3 should be allowed");
        }

        // 4th request should be rate limited
        var limitedResponse = await client.GetAsync("/");
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task AuthenticatedPolicy_EnforcesUserLimit_Returns429AfterLimit()
    {
        // sub=user-test-2 — unique key so it doesn't collide with other tests
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateJwt("user-test-2"));

        // First 5 requests should succeed (authenticated limit = 5 in test fixture)
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"request {i + 1} of 5 should be allowed");
        }

        // 6th request should be rate limited
        var limitedResponse = await client.GetAsync("/");
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task AuthenticatedPolicy_BypassesAnonymousLimit_HigherLimitApplied()
    {
        // X-Forwarded-For: 10.1.1.3 and sub=user-test-3 — unique keys
        // Sends 4 requests: exceeds anonymous limit (3) but not authenticated limit (5)
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.3");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateJwt("user-test-3"));

        for (var i = 0; i < 4; i++)
        {
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"authenticated user should use the 5-request limit, not the 3-request limit; " +
                $"request {i + 1} should be allowed");
        }
    }

    private static string CreateJwt(string sub)
    {
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncode($"""{"sub":"{sub}"}""");
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(string input) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
```

- [ ] **Step 8: Add test project to solution**

```bash
dotnet sln TCGTrading.sln add tests/ApiGateway.Tests/TCGTrading.ApiGateway.Tests.csproj \
    --solution-folder tests/ApiGateway.Tests
```

- [ ] **Step 9: Verify tests compile and fail at runtime**

```bash
dotnet test tests/ApiGateway.Tests/TCGTrading.ApiGateway.Tests.csproj
```

Expected: Build succeeds. All 3 tests run and FAIL with assertion errors like `Expected HttpStatusCode.TooManyRequests but found OK`. This confirms: tests compile, Redis container starts, but rate limiting is not installed yet.

- [ ] **Step 10: Commit**

```bash
git add tests/ApiGateway.Tests/ src/ApiGateway/Program.cs TCGTrading.sln
git commit -m "test(rate-limiting): add ApiGateway.Tests project with failing integration tests"
```

---

### Task 3: Implement `RateLimitingExtensions` and wire into `Program.cs`

Create the rate limiting infrastructure that makes the Task 2 tests pass.

**Files:**
- Create: `src/ApiGateway/Infrastructure/RateLimiting/RateLimitingExtensions.cs`
- Modify: `src/ApiGateway/Program.cs` — call `AddRateLimiting()` and `UseRateLimiter()`

**Interfaces:**
- Consumes: `RateLimitConfig` (created in Task 2), `RedisRateLimiting.AspNetCore` (`RateLimitPartition.GetRedisSlidingWindowLimiter`, `RedisSlidingWindowRateLimiterOptions`), `StackExchange.Redis` (`IConnectionMultiplexer`, `ConnectionMultiplexer`), `Microsoft.AspNetCore.RateLimiting` (`AddRateLimiter`, `UseRateLimiter`), `System.Threading.RateLimiting` (`PartitionedRateLimiter`, `MetadataName`)
- Produces: `RateLimitingExtensions.AddRateLimiting(IHostApplicationBuilder)` — wired into `Program.cs`; makes Task 2 tests pass

- [ ] **Step 1: Create `src/ApiGateway/Infrastructure/RateLimiting/RateLimitingExtensions.cs`**

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RedisRateLimiting;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace TCGTrading.ApiGateway.Infrastructure.RateLimiting;

public static class RateLimitingExtensions
{
    public static IHostApplicationBuilder AddRateLimiting(
        this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration["Redis:ConnectionString"]
            ?? "localhost:6379";

        builder.Services.Configure<RateLimitConfig>(
            builder.Configuration.GetSection("RateLimit"));

        builder.Services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                await context.HttpContext.Response.WriteAsync(
                    "Rate limit exceeded.", cancellationToken);
            };

            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var cfg = httpContext.RequestServices
                        .GetRequiredService<IOptions<RateLimitConfig>>().Value;
                    var redis = httpContext.RequestServices
                        .GetRequiredService<IConnectionMultiplexer>();

                    var sub = ExtractSub(httpContext);
                    if (sub is not null)
                    {
                        return RateLimitPartition.GetRedisSlidingWindowLimiter(
                            partitionKey: $"rl:auth:{sub}",
                            factory: _ => new RedisSlidingWindowRateLimiterOptions
                            {
                                ConnectionMultiplexerFactory = () => redis,
                                PermitLimit = cfg.AuthenticatedPermitLimit,
                                Window = TimeSpan.FromSeconds(cfg.WindowSeconds),
                            });
                    }

                    var ip = httpContext.Request.Headers["X-Forwarded-For"]
                                 .FirstOrDefault()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString()
                             ?? "unknown";

                    return RateLimitPartition.GetRedisSlidingWindowLimiter(
                        partitionKey: $"rl:anon:{ip}",
                        factory: _ => new RedisSlidingWindowRateLimiterOptions
                        {
                            ConnectionMultiplexerFactory = () => redis,
                            PermitLimit = cfg.AnonymousPermitLimit,
                            Window = TimeSpan.FromSeconds(cfg.WindowSeconds),
                        });
                });
        });

        return builder;
    }

    private static string? ExtractSub(HttpContext httpContext)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..];
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            var payload = parts[1];
            var padded = payload.Length % 4 switch
            {
                2 => payload + "==",
                3 => payload + "=",
                _ => payload
            };
            var base64 = padded.Replace('-', '+').Replace('_', '/');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var sub)
                ? sub.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 2: Run `dotnet build` to catch any compilation errors before touching `Program.cs`**

```bash
dotnet build src/ApiGateway/TCGTrading.ApiGateway.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

If you see a warning about `TreatWarningsAsErrors`, fix it before continuing.

- [ ] **Step 3: Update `src/ApiGateway/Program.cs` to wire rate limiting**

Replace the full contents of `src/ApiGateway/Program.cs`:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.ApiGateway.Infrastructure.RateLimiting;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("api-gateway");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddRateLimiting();

var app = builder.Build();

app.UseRateLimiter();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();

// Exposes Program class to WebApplicationFactory<Program> in test projects
public partial class Program { }
```

Note: `UseRateLimiter()` must come before route registrations (`MapGet`, etc.) so every route is covered.

- [ ] **Step 4: Run the tests**

```bash
dotnet test tests/ApiGateway.Tests/TCGTrading.ApiGateway.Tests.csproj -v normal
```

Expected output:
```
Passed  AnonymousPolicy_EnforcesIpLimit_Returns429AfterLimit
Passed  AuthenticatedPolicy_EnforcesUserLimit_Returns429AfterLimit
Passed  AuthenticatedPolicy_BypassesAnonymousLimit_HigherLimitApplied

Test Run Successful.
Total tests: 3
     Passed: 3
     Failed: 0
```

If tests fail: check that the `ApiGatewayFixture` successfully replaced `IConnectionMultiplexer` — add a debug log or check that `_redis.GetConnectionString()` returns a non-empty string.

- [ ] **Step 5: Run the full test suite to confirm no regressions**

```bash
dotnet test TCGTrading.sln --no-build -c Release
```

Expected: all 38 tests pass (35 existing + 3 new).

- [ ] **Step 6: Commit**

```bash
git add src/ApiGateway/Infrastructure/ src/ApiGateway/Program.cs
git commit -m "feat(rate-limiting): add Redis-backed sliding window rate limiter to ApiGateway"
```

---

## Summary

| Task | Deliverable | Verified by |
|------|-------------|-------------|
| 1 | Packages + config | `dotnet restore` succeeds |
| 2 | Test project + failing tests | Compile error on missing `RateLimitConfig` |
| 3 | `RateLimitingExtensions` + `Program.cs` wiring | 3/3 integration tests pass, 38/38 total |
