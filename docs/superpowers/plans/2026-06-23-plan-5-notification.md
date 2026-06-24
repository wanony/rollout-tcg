# Notification Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Notification service — consumes `ListingPurchasedEvent` from RabbitMQ and creates in-app notifications for both buyer and seller. Notifications are stored in PostgreSQL and exposed via REST. A SignalR hub pushes them to connected browser clients in real time.

**Architecture:** Clean architecture in `src/Notification/`. MassTransit consumer handles `ListingPurchasedEvent` → writes two `UserNotification` rows → calls `IHubContext<NotificationHub>` to push to connected users. `UserIdProvider` maps a `userId` query-string param on the hub connection to a SignalR user identity (simple, avoids JWT middleware in this service for v1). REST endpoints: `GET /notifications?userId=` and `POST /notifications/{id}/read`.

**Tech Stack:** .NET 8, EF Core 8 + Npgsql, MassTransit 8 + RabbitMQ, ASP.NET Core SignalR (built-in, no extra package), NSubstitute (unit tests), Testcontainers.PostgreSql + Testcontainers.RabbitMq, xUnit, FluentAssertions

## Global Constraints

- Target framework: `net8.0` (from `Directory.Build.props`)
- Test project target framework: `net10.0`
- Central package management: versions in `Directory.Packages.props` only
- `TreatWarningsAsErrors=true` — zero warnings
- `NuGetAuditMode=direct` in test csproj
- Test projects require `test.runsettings` + `GlobalUsings.cs` with `global using Xunit;`
- `public partial class Program {}` required at bottom of `src/Notification/Program.cs`
- DB name: `db_notification` — **not yet in** `infra/postgres/init/01-create-databases.sql`; must be added in Task 1
- Connection string config key: `ConnectionStrings:NotificationDb`
- RabbitMQ config keys: `RabbitMq:Host`, `RabbitMq:Username`, `RabbitMq:Password`
- Service port: `5004`
- SignalR hub path: `/hubs/notifications`
- SignalR userId query param: `userId` (e.g. `ws://host/hubs/notifications?userId=<guid>`)
- Entity class name: `UserNotification` (avoids namespace collision with `TCGTrading.Notification.*`)
- `dotnet-ef` tool required: `dotnet tool install -g dotnet-ef`
- `ListingPurchasedEvent` is in `TCGTrading.SharedKernel.Contracts` — do not redefine

---

## File Map

**New files:**
```
src/Notification/Domain/Entities/UserNotification.cs
src/Notification/Domain/Enums/NotificationType.cs
src/Notification/Application/Interfaces/INotificationRepository.cs
src/Notification/Application/DTOs/NotificationResponse.cs
src/Notification/Infrastructure/Persistence/NotificationDbContext.cs
src/Notification/Infrastructure/Persistence/NotificationDbContextFactory.cs
src/Notification/Infrastructure/Persistence/NotificationRepository.cs
src/Notification/Infrastructure/Persistence/PersistenceExtensions.cs
src/Notification/Infrastructure/Persistence/Migrations/           (generated)
src/Notification/Infrastructure/Messaging/ListingPurchasedConsumer.cs
src/Notification/Infrastructure/Messaging/MessagingExtensions.cs
src/Notification/Infrastructure/Hubs/NotificationHub.cs
src/Notification/Infrastructure/Hubs/UserIdProvider.cs
tests/Notification.Tests/TCGTrading.Notification.Tests.csproj
tests/Notification.Tests/test.runsettings
tests/Notification.Tests/GlobalUsings.cs
tests/Notification.Tests/Fixtures/NotificationFixture.cs
tests/Notification.Tests/Integration/NotificationEndpointTests.cs
tests/Notification.Tests/Unit/ListingPurchasedConsumerTests.cs
```

**Modified files:**
```
src/Notification/TCGTrading.Notification.Api.csproj    — add EF Core + MassTransit packages
src/Notification/appsettings.json                      — add ConnectionStrings + RabbitMq + CORS sections
src/Notification/appsettings.Development.json          — dev connection strings
src/Notification/Program.cs                            — implement endpoints + SignalR + messaging
src/Notification/Dockerfile                            — add curl
docker-compose.yml                                     — add notification service
infra/postgres/init/01-create-databases.sql            — add db_notification
TCGTrading.sln                                         — add Notification.Tests project
```

---

### Task 1: Packages, config, DB init, and docker-compose

**Files:**
- Modify: `src/Notification/TCGTrading.Notification.Api.csproj`
- Modify: `src/Notification/appsettings.json`
- Create: `src/Notification/appsettings.Development.json`
- Modify: `src/Notification/Dockerfile`
- Modify: `infra/postgres/init/01-create-databases.sql`
- Modify: `docker-compose.yml`

**Interfaces:**
- Produces: EF Core + MassTransit packages available; `db_notification` created on next Postgres init; notification service in docker-compose at port 5004

- [ ] **Step 1: Add packages to Notification csproj**

Replace the entire contents of `src/Notification/TCGTrading.Notification.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../SharedKernel/TCGTrading.SharedKernel.csproj" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="MassTransit" />
    <PackageReference Include="MassTransit.RabbitMQ" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add appsettings.json**

Replace `src/Notification/appsettings.json`:

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
  "ConnectionStrings": {
    "NotificationDb": "Host=postgres;Port=5432;Database=db_notification;Username=tcg;Password=dev"
  },
  "RabbitMq": {
    "Host": "rabbitmq",
    "Username": "tcg",
    "Password": "dev"
  },
  "Cors": {
    "AllowedOrigin": "http://localhost:3000"
  }
}
```

- [ ] **Step 3: Create appsettings.Development.json**

Create `src/Notification/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "NotificationDb": "Host=localhost;Port=5432;Database=db_notification;Username=tcg;Password=dev"
  },
  "RabbitMq": {
    "Host": "localhost",
    "Username": "tcg",
    "Password": "dev"
  },
  "Telemetry": {
    "OtlpEndpoint": "http://localhost:4317",
    "LokiEndpoint": "http://localhost:3100"
  }
}
```

- [ ] **Step 4: Add db_notification to postgres init script**

Open `infra/postgres/init/01-create-databases.sql`. Add at the end:

```sql
CREATE DATABASE db_notification;
```

> **Note:** This script only runs when the postgres volume is first created. If postgres is already running with existing data, you must run `docker compose down -v && docker compose up postgres -d` to recreate the volume and trigger the init script. This will **delete all existing dev data**.

- [ ] **Step 5: Add curl to Dockerfile**

Replace `src/Notification/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/Notification/TCGTrading.Notification.Api.csproj src/Notification/

RUN dotnet restore src/Notification/TCGTrading.Notification.Api.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/Notification/ src/Notification/

RUN dotnet publish src/Notification/TCGTrading.Notification.Api.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.Notification.Api.dll"]
```

- [ ] **Step 6: Add notification service to docker-compose.yml**

Open `docker-compose.yml`. Add after the `marketplace` block:

```yaml
  notification:
    build:
      context: .
      dockerfile: src/Notification/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:5004
      ConnectionStrings__NotificationDb: "Host=postgres;Port=5432;Database=db_notification;Username=tcg;Password=dev"
      RabbitMq__Host: rabbitmq
      RabbitMq__Username: tcg
      RabbitMq__Password: dev
      Cors__AllowedOrigin: "http://localhost:3000"
    ports:
      - "5004:5004"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:5004/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 20s
```

- [ ] **Step 7: Build**

```bash
dotnet build src/Notification/TCGTrading.Notification.Api.csproj
```

Expected: Build succeeded.

---

### Task 2: Domain and Application layers

**Files:**
- Create: `src/Notification/Domain/Enums/NotificationType.cs`
- Create: `src/Notification/Domain/Entities/UserNotification.cs`
- Create: `src/Notification/Application/Interfaces/INotificationRepository.cs`
- Create: `src/Notification/Application/DTOs/NotificationResponse.cs`

**Interfaces:**
- Produces:
  - `UserNotification.Create(userId, title, message, type, referenceId)` → `UserNotification`
  - `userNotification.MarkRead()` — sets `IsRead = true`
  - `INotificationRepository` with `AddAsync`, `GetByUserIdAsync`, `GetByIdAsync`, `UpdateAsync`

- [ ] **Step 1: Create NotificationType enum**

Create `src/Notification/Domain/Enums/NotificationType.cs`:

```csharp
namespace TCGTrading.Notification.Domain.Enums;

public enum NotificationType { ListingPurchased, ListingSold }
```

- [ ] **Step 2: Create UserNotification entity**

Create `src/Notification/Domain/Entities/UserNotification.cs`:

```csharp
using TCGTrading.Notification.Domain.Enums;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.Notification.Domain.Entities;

public class UserNotification : Entity
{
    public Guid UserId { get; private init; }
    public string Title { get; private init; } = string.Empty;
    public string Message { get; private init; } = string.Empty;
    public NotificationType Type { get; private init; }
    public Guid? ReferenceId { get; private init; }
    public bool IsRead { get; private set; }

    private UserNotification() { } // EF Core

    public static UserNotification Create(
        Guid userId, string title, string message,
        NotificationType type, Guid? referenceId = null)
        => new()
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ReferenceId = referenceId,
            IsRead = false
        };

    public void MarkRead() => IsRead = true;
}
```

- [ ] **Step 3: Create INotificationRepository**

Create `src/Notification/Application/Interfaces/INotificationRepository.cs`:

```csharp
using TCGTrading.Notification.Domain.Entities;

namespace TCGTrading.Notification.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(UserNotification notification, CancellationToken ct = default);
    Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserNotification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(UserNotification notification, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create NotificationResponse DTO**

Create `src/Notification/Application/DTOs/NotificationResponse.cs`:

```csharp
namespace TCGTrading.Notification.Application.DTOs;

public record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    string Type,
    Guid? ReferenceId,
    bool IsRead,
    DateTime CreatedAt);
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Notification/TCGTrading.Notification.Api.csproj
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Notification/
git commit -m "feat(notification): add domain entities and application layer"
```

---

### Task 3: Infrastructure — EF Core persistence

**Files:**
- Create: `src/Notification/Infrastructure/Persistence/NotificationDbContext.cs`
- Create: `src/Notification/Infrastructure/Persistence/NotificationDbContextFactory.cs`
- Create: `src/Notification/Infrastructure/Persistence/NotificationRepository.cs`
- Create: `src/Notification/Infrastructure/Persistence/PersistenceExtensions.cs`
- Create: `src/Notification/Infrastructure/Persistence/Migrations/` (generated)

**Interfaces:**
- Produces: `AddPersistence()` on `IHostApplicationBuilder`; `NotificationDbContext` with `UserNotifications` DbSet

- [ ] **Step 1: Create NotificationDbContext**

Create `src/Notification/Infrastructure/Persistence/NotificationDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TCGTrading.Notification.Domain.Entities;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserNotification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
            e.HasIndex(x => x.UserId);
        });
    }
}
```

- [ ] **Step 2: Create NotificationDbContextFactory**

Create `src/Notification/Infrastructure/Persistence/NotificationDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=db_notification;Username=tcg;Password=dev")
            .Options;
        return new NotificationDbContext(opts);
    }
}
```

- [ ] **Step 3: Create NotificationRepository**

Create `src/Notification/Infrastructure/Persistence/NotificationRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task AddAsync(UserNotification notification, CancellationToken ct = default)
    {
        await db.UserNotifications.AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.UserNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task<UserNotification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.UserNotifications.FindAsync([id], ct);

    public async Task UpdateAsync(UserNotification notification, CancellationToken ct = default)
    {
        db.UserNotifications.Update(notification);
        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Create PersistenceExtensions**

Create `src/Notification/Infrastructure/Persistence/PersistenceExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCGTrading.Notification.Application.Interfaces;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<NotificationDbContext>(opts =>
            opts.UseNpgsql(builder.Configuration.GetConnectionString("NotificationDb")));
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        return builder;
    }
}
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Notification/TCGTrading.Notification.Api.csproj
```

- [ ] **Step 6: Generate migration**

`db_notification` must exist. If starting fresh: `docker compose down -v && docker compose up postgres -d` (waits ~10s for init scripts). If you already created it manually: just ensure postgres is running.

```bash
dotnet ef migrations add InitialCreate \
  --project src/Notification/TCGTrading.Notification.Api.csproj \
  --output-dir Infrastructure/Persistence/Migrations
```

Expected: output ending with `Done.`

- [ ] **Step 7: Commit**

```bash
git add src/Notification/
git commit -m "feat(notification): add EF Core persistence and migration"
```

---

### Task 4: Messaging and SignalR hub

**Files:**
- Create: `src/Notification/Infrastructure/Hubs/NotificationHub.cs`
- Create: `src/Notification/Infrastructure/Hubs/UserIdProvider.cs`
- Create: `src/Notification/Infrastructure/Messaging/ListingPurchasedConsumer.cs`
- Create: `src/Notification/Infrastructure/Messaging/MessagingExtensions.cs`

**Interfaces:**
- Produces:
  - `NotificationHub` at `/hubs/notifications` — clients call `hub.on("notification", handler)`
  - `UserIdProvider` — maps `Context.GetHttpContext().Request.Query["userId"]` to SignalR user identity
  - `ListingPurchasedConsumer` — creates buyer + seller `UserNotification`, saves both, pushes via `IHubContext<NotificationHub>`
  - `AddMessaging()` on `IHostApplicationBuilder`

- [ ] **Step 1: Create NotificationHub**

Create `src/Notification/Infrastructure/Hubs/NotificationHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace TCGTrading.Notification.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    // Clients call hub.on("notification", handler) to receive pushed notifications.
    // No server-side methods needed in v1 — hub is push-only.
}
```

- [ ] **Step 2: Create UserIdProvider**

Create `src/Notification/Infrastructure/Hubs/UserIdProvider.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace TCGTrading.Notification.Infrastructure.Hubs;

// ponytail: userId from query string; replace with JWT sub claim when auth middleware is added
public class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.GetHttpContext()?.Request.Query["userId"].ToString();
}
```

- [ ] **Step 3: Create ListingPurchasedConsumer**

Create `src/Notification/Infrastructure/Messaging/ListingPurchasedConsumer.cs`:

```csharp
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TCGTrading.Notification.Application.DTOs;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Domain.Enums;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Notification.Infrastructure.Messaging;

public class ListingPurchasedConsumer(
    INotificationRepository repo,
    IHubContext<NotificationHub> hub) : IConsumer<ListingPurchasedEvent>
{
    public async Task Consume(ConsumeContext<ListingPurchasedEvent> context)
    {
        var e = context.Message;

        var buyerNotif = UserNotification.Create(
            e.BuyerId,
            title: "Purchase confirmed",
            message: $"You purchased a card for ${e.PurchasePriceUsd:F2}.",
            NotificationType.ListingPurchased,
            referenceId: e.ListingId);

        var sellerNotif = UserNotification.Create(
            e.SellerId,
            title: "Your listing sold",
            message: $"Your listing sold for ${e.PurchasePriceUsd:F2}.",
            NotificationType.ListingSold,
            referenceId: e.ListingId);

        await repo.AddAsync(buyerNotif, context.CancellationToken);
        await repo.AddAsync(sellerNotif, context.CancellationToken);

        await PushAsync(e.BuyerId, buyerNotif);
        await PushAsync(e.SellerId, sellerNotif);
    }

    private async Task PushAsync(Guid userId, UserNotification notif)
    {
        var response = new NotificationResponse(
            notif.Id, notif.UserId, notif.Title, notif.Message,
            notif.Type.ToString(), notif.ReferenceId, notif.IsRead, notif.CreatedAt);

        await hub.Clients.User(userId.ToString()).SendAsync("notification", response);
    }
}
```

- [ ] **Step 4: Create MessagingExtensions**

Create `src/Notification/Infrastructure/Messaging/MessagingExtensions.cs`:

```csharp
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TCGTrading.Notification.Infrastructure.Messaging;

namespace TCGTrading.Notification.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<ListingPurchasedConsumer>();
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(config["RabbitMq:Host"]!, "/", h =>
                {
                    h.Username(config["RabbitMq:Username"] ?? "guest");
                    h.Password(config["RabbitMq:Password"] ?? "guest");
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });
        return builder;
    }
}
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Notification/TCGTrading.Notification.Api.csproj
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Notification/Infrastructure/
git commit -m "feat(notification): add SignalR hub and MassTransit consumer"
```

---

### Task 5: Program.cs — wire up and expose endpoints

**Files:**
- Modify: `src/Notification/Program.cs`

**Interfaces:**
- Produces:
  - `GET /notifications?userId={guid}` → 200 with `NotificationResponse[]`
  - `POST /notifications/{id}/read` → 200 with updated `NotificationResponse` or 404
  - `GET /hubs/notifications` — SignalR hub endpoint (WebSocket + long polling)
  - `GET /health` → 200

- [ ] **Step 1: Replace Program.cs**

Replace the entire contents of `src/Notification/Program.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Notification.Application.DTOs;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.Notification.Infrastructure.Messaging;
using TCGTrading.Notification.Infrastructure.Persistence;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("notification");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();
builder.AddMessaging();

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, UserIdProvider>();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials())); // Required for SignalR

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok());

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/notifications", async (
    Guid userId,
    INotificationRepository repo,
    CancellationToken ct) =>
{
    var items = await repo.GetByUserIdAsync(userId, ct);
    return Results.Ok(items.Select(n => n.ToResponse()));
});

app.MapPost("/notifications/{id:guid}/read", async (
    Guid id,
    INotificationRepository repo,
    CancellationToken ct) =>
{
    var notif = await repo.GetByIdAsync(id, ct);
    if (notif is null) return Results.NotFound();
    notif.MarkRead();
    await repo.UpdateAsync(notif, ct);
    return Results.Ok(notif.ToResponse());
});

app.Run();

public partial class Program { }

static class NotificationExtensions
{
    public static NotificationResponse ToResponse(this UserNotification n) => new(
        n.Id, n.UserId, n.Title, n.Message,
        n.Type.ToString(), n.ReferenceId, n.IsRead, n.CreatedAt);
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Notification/TCGTrading.Notification.Api.csproj
```

Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Smoke test**

With postgres (including `db_notification`) and rabbitmq running:

```bash
dotnet run --project src/Notification/TCGTrading.Notification.Api.csproj
```

```bash
curl "http://localhost:5004/notifications?userId=11111111-1111-1111-1111-111111111111"
```

Expected: HTTP 200 with `[]`.

- [ ] **Step 4: Commit**

```bash
git add src/Notification/Program.cs
git commit -m "feat(notification): implement notification endpoints and SignalR hub"
```

---

### Task 6: Test project and tests

**Files:**
- Create: `tests/Notification.Tests/TCGTrading.Notification.Tests.csproj`
- Create: `tests/Notification.Tests/test.runsettings`
- Create: `tests/Notification.Tests/GlobalUsings.cs`
- Create: `tests/Notification.Tests/Fixtures/NotificationFixture.cs`
- Create: `tests/Notification.Tests/Integration/NotificationEndpointTests.cs`
- Create: `tests/Notification.Tests/Unit/ListingPurchasedConsumerTests.cs`
- Modify: `TCGTrading.sln`

**Interfaces:**
- Consumes: `Program` from `TCGTrading.Notification.Api`; `ListingPurchasedConsumer`; `INotificationRepository`; `NotificationResponse` DTO; `ListingPurchasedEvent` from SharedKernel

- [ ] **Step 1: Create test csproj**

Create `tests/Notification.Tests/TCGTrading.Notification.Tests.csproj`:

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
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Testcontainers.RabbitMq" />
    <PackageReference Include="NSubstitute" />
    <ProjectReference Include="../../src/Notification/TCGTrading.Notification.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create test.runsettings**

Create `tests/Notification.Tests/test.runsettings`:

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

- [ ] **Step 3: Create GlobalUsings.cs**

Create `tests/Notification.Tests/GlobalUsings.cs`:

```csharp
global using Xunit;
```

- [ ] **Step 4: Create NotificationFixture**

Create `tests/Notification.Tests/Fixtures/NotificationFixture.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace TCGTrading.Notification.Tests;

public class NotificationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("db_notification")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    public async Task InitializeAsync()
        => await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

    public new async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitmq.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:NotificationDb"] = _postgres.GetConnectionString(),
                ["RabbitMq:Host"] = _rabbitmq.Hostname,
                ["RabbitMq:Username"] = _rabbitmq.Username,
                ["RabbitMq:Password"] = _rabbitmq.Password
            }));
    }
}
```

- [ ] **Step 5: Write integration endpoint tests**

Create `tests/Notification.Tests/Integration/NotificationEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.Notification.Application.DTOs;

namespace TCGTrading.Notification.Tests.Integration;

public class NotificationEndpointTests(NotificationFixture fixture)
    : IClassFixture<NotificationFixture>
{
    [Fact]
    public async Task GetNotifications_EmptyUser_ReturnsEmptyArray()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync($"/notifications?userId={Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<NotificationResponse[]>();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_UnknownId_Returns404()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsync(
            $"/notifications/{Guid.NewGuid()}/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 6: Write consumer unit test**

Create `tests/Notification.Tests/Unit/ListingPurchasedConsumerTests.cs`:

```csharp
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.Notification.Infrastructure.Messaging;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Notification.Tests.Unit;

public class ListingPurchasedConsumerTests
{
    [Fact]
    public async Task Consume_CreatesNotifications_ForBothBuyerAndSeller()
    {
        var repo = Substitute.For<INotificationRepository>();
        var hub = Substitute.For<IHubContext<NotificationHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.User(Arg.Any<string>()).Returns(proxy);

        var consumer = new ListingPurchasedConsumer(repo, hub);

        var @event = new ListingPurchasedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid(),
            BuyerId: Guid.NewGuid(),
            SellerId: Guid.NewGuid(),
            PurchasePriceUsd: 15.00m);

        var ctx = Substitute.For<ConsumeContext<ListingPurchasedEvent>>();
        ctx.Message.Returns(@event);
        ctx.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(ctx);

        await repo.Received(2).AddAsync(
            Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());

        await proxy.Received(2).SendCoreAsync(
            "notification", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 7: Add to solution**

```bash
dotnet sln add tests/Notification.Tests/TCGTrading.Notification.Tests.csproj
```

- [ ] **Step 8: Run tests**

```bash
dotnet test tests/Notification.Tests/TCGTrading.Notification.Tests.csproj -v minimal
```

Expected: All 3 tests pass (2 integration + 1 unit).

- [ ] **Step 9: Commit**

```bash
git add tests/Notification.Tests/ TCGTrading.sln
git commit -m "test(notification): add integration and unit tests"
```
