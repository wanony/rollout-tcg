# TCG Trading Platform — Plan 2: Identity Service

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Duende IdentityServer 7 service that issues OAuth2/OIDC tokens for all other platform services, persists users via ASP.NET Core Identity + PostgreSQL, and exposes a user registration endpoint.

**Architecture:** Single `src/IdentityService/` ASP.NET Core project hosting Duende IdentityServer. Identity config (clients, scopes, resources) is code-managed via `Config.cs` — no DB config store needed. Users and their credentials are stored in `db_identity` via ASP.NET Core Identity + EF Core. In Docker, the service is reachable at `http://identity-service:5001`. Other services validate JWTs against the JWKS endpoint at `/.well-known/openid-configuration`.

**Tech Stack:** Duende.IdentityServer 7.0.6, Duende.IdentityServer.AspNetIdentity 7.0.6, Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.0, Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0, Microsoft.AspNetCore.Mvc.Testing 8.0.0, Testcontainers.PostgreSql 3.10.0, xUnit 2.9.0

## Global Constraints

- Target framework: `net8.0` (set in `Directory.Build.props` — do not set in individual csproj files)
- C# language version: `12`
- `Nullable=enable`, `TreatWarningsAsErrors=true` (from `Directory.Build.props`)
- Central package management: `Directory.Packages.props` holds all versions; individual csproj `<PackageReference>` entries have NO version attribute
- **Machine runs .NET SDK 10.0.101 targeting net8.0** — `test.runsettings` + `GlobalUsings.cs` required in every test project (copy pattern from `tests/SharedKernel.Tests/`)
- Duende IdentityServer Community Edition is free for non-commercial use — no license key needed for dev
- Developer signing credential (`AddDeveloperSigningCredential()`) is used in all environments — note in code that production requires a proper certificate from Key Vault
- `AddDeveloperSigningCredential()` regenerates the key on each restart — acceptable for dev; persisted key storage is Plan 7 scope (API Gateway)
- No HTTPS in Docker dev environment — `ASPNETCORE_URLS=http://+:5001`
- EF Core migrations run automatically on startup via `db.Database.MigrateAsync()` — no manual `dotnet ef database update` required in Docker
- `Program` class exposed as `public partial class Program { }` at end of `Program.cs` to enable `WebApplicationFactory<Program>` in tests
- ROPC (Resource Owner Password Credentials) grant enabled on `test-client` only — used exclusively for integration tests; SPA client uses Authorization Code + PKCE

---

## File Map

```
Directory.Packages.props                                 ← modify: add 4 new PackageVersion entries
Directory.Build.props                                    ← no changes

src/IdentityService/
  TCGTrading.IdentityService.csproj                      ← modify: add package refs, tool manifest ref
  Program.cs                                             ← replace: full IS + Identity wiring
  Config.cs                                              ← create: clients, scopes, identity resources
  Models/
    ApplicationUser.cs                                   ← create: extends IdentityUser, adds DisplayName
  Data/
    AppDbContext.cs                                      ← create: IdentityDbContext<ApplicationUser>
  Endpoints/
    AccountEndpoints.cs                                  ← create: POST /account/register
  Migrations/                                            ← generated: dotnet ef migrations add
  appsettings.json                                       ← modify: add ConnectionStrings section
  appsettings.Development.json                           ← create: dev connection string
  Dockerfile                                             ← create: multi-stage build

docker-compose.yml                                       ← modify: add identity-service block

tests/IdentityService.Tests/
  TCGTrading.IdentityService.Tests.csproj                ← create
  GlobalUsings.cs                                        ← create: global using Xunit;
  test.runsettings                                       ← create: DOTNET_ROLL_FORWARD=LatestMajor
  runtimeconfig.template.json                            ← create: rollForward LatestMajor
  Fixtures/
    IdentityServiceFixture.cs                            ← create: WebApplicationFactory + Testcontainers
  AccountEndpointsTests.cs                               ← create: register endpoint integration tests
  TokenFlowTests.cs                                      ← create: ROPC token issuance + JWT validation
```

---

### Task 1: Package Setup + Domain Model

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/IdentityService/TCGTrading.IdentityService.csproj`
- Create: `src/IdentityService/Models/ApplicationUser.cs`
- Create: `src/IdentityService/Data/AppDbContext.cs`
- Modify: `src/IdentityService/appsettings.json`
- Create: `src/IdentityService/appsettings.Development.json`

**Interfaces:**
- Produces:
  - `ApplicationUser : IdentityUser` with `string? DisplayName`
  - `AppDbContext : IdentityDbContext<ApplicationUser>`
  - `IdentityDb` connection string in appsettings

- [ ] **Step 1: Add new packages to Directory.Packages.props**

Open `Directory.Packages.props` and add these four entries inside the existing `<ItemGroup>`:
```xml
<PackageVersion Include="Duende.IdentityServer" Version="7.0.6" />
<PackageVersion Include="Duende.IdentityServer.AspNetIdentity" Version="7.0.6" />
<PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />
<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
```

- [ ] **Step 2: Update IdentityService.csproj**

Replace the contents of `src/IdentityService/TCGTrading.IdentityService.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Duende.IdentityServer" />
    <PackageReference Include="Duende.IdentityServer.AspNetIdentity" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create ApplicationUser**

Create `src/IdentityService/Models/ApplicationUser.cs`:
```csharp
using Microsoft.AspNetCore.Identity;

namespace TCGTrading.IdentityService.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
```

- [ ] **Step 4: Create AppDbContext**

Create `src/IdentityService/Data/AppDbContext.cs`:
```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TCGTrading.IdentityService.Models;

namespace TCGTrading.IdentityService.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}
```

- [ ] **Step 5: Add connection string to appsettings.json**

Replace `src/IdentityService/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Duende": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "IdentityDb": "Host=localhost;Port=5432;Database=db_identity;Username=tcg;Password=dev"
  }
}
```

- [ ] **Step 6: Create appsettings.Development.json**

Create `src/IdentityService/appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Duende": "Information"
    }
  }
}
```

- [ ] **Step 7: Verify solution still builds**

```bash
dotnet build TCGTrading.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add Directory.Packages.props src/IdentityService/
git commit -m "feat(identity): add packages, ApplicationUser, AppDbContext"
```

---

### Task 2: Duende IS Configuration + Program.cs Wiring

**Files:**
- Create: `src/IdentityService/Config.cs`
- Replace: `src/IdentityService/Program.cs`

**Interfaces:**
- Consumes: `ApplicationUser` (Task 1), `AppDbContext` (Task 1)
- Produces:
  - IS discovery at `/.well-known/openid-configuration`
  - Token endpoint at `/connect/token`
  - JWKS at `/.well-known/openid-configuration/jwks`
  - `spa` client (Authorization Code + PKCE, no secret)
  - `test-client` client (ROPC, secret `test-secret`)
  - API scope `tcg.full`
  - Identity resources: `openid`, `profile`, `email`

- [ ] **Step 1: Create Config.cs**

Create `src/IdentityService/Config.cs`:
```csharp
using Duende.IdentityServer.Models;

namespace TCGTrading.IdentityService;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("tcg.full", "TCG Trading Full Access")
    ];

    public static IEnumerable<Client> Clients =>
    [
        // SPA — Authorization Code + PKCE, no client secret (browser-safe)
        new Client
        {
            ClientId = "spa",
            ClientName = "TCG Trading SPA",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "http://localhost:3000/callback" },
            PostLogoutRedirectUris = { "http://localhost:3000" },
            AllowedCorsOrigins = { "http://localhost:3000" },
            AllowedScopes = { "openid", "profile", "email", "tcg.full" },
            AllowOfflineAccess = true
        },
        // Test client — ROPC for integration tests ONLY, never expose to production
        new Client
        {
            ClientId = "test-client",
            ClientSecrets = { new Secret("test-secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            AllowedScopes = { "openid", "profile", "email", "tcg.full" }
        }
    ];
}
```

- [ ] **Step 2: Replace Program.cs**

Replace `src/IdentityService/Program.cs`:
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TCGTrading.IdentityService;
using TCGTrading.IdentityService.Data;
using TCGTrading.IdentityService.Endpoints;
using TCGTrading.IdentityService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Developer signing credential auto-generates a key on each restart.
// Production: replace with AddSigningCredential() using cert from Key Vault.
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
})
.AddDeveloperSigningCredential()
.AddInMemoryIdentityResources(Config.IdentityResources)
.AddInMemoryApiScopes(Config.ApiScopes)
.AddInMemoryClients(Config.Clients)
.AddAspNetIdentity<ApplicationUser>();

builder.Services.AddAuthorization();

var app = builder.Build();

// Migrate on startup — idempotent, safe in Docker restarts
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseIdentityServer();
app.UseAuthorization();

app.MapAccountEndpoints();

app.Run();

// Exposes Program class to WebApplicationFactory<Program> in test projects
public partial class Program { }
```

- [ ] **Step 3: Create stub AccountEndpoints so it compiles**

Create `src/IdentityService/Endpoints/AccountEndpoints.cs` (full implementation in Task 3 — stub now so Program.cs compiles):
```csharp
namespace TCGTrading.IdentityService.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        return app;
    }
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build TCGTrading.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Create EF Core tool manifest (if not already present)**

```bash
ls .config/dotnet-tools.json 2>/dev/null || dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.*
```
Expected: manifest created at `.config/dotnet-tools.json`, `dotnet-ef` installed.

- [ ] **Step 6: Create EF Core migration**

Postgres must be running for this step (`docker compose ps` — if not running, `docker compose up -d postgres`).

```bash
cd src/IdentityService
dotnet ef migrations add InitialCreate --context AppDbContext --output-dir Migrations
cd ../..
```
Expected: `Build started... Build succeeded.` and `Done. To undo this action, use 'ef migrations remove'`
Files created: `src/IdentityService/Migrations/YYYYMMDDHHMMSS_InitialCreate.cs` and `src/IdentityService/Migrations/AppDbContextModelSnapshot.cs`

- [ ] **Step 7: Verify migration applies against local Postgres**

```bash
cd src/IdentityService
dotnet ef database update --context AppDbContext
cd ../..
```
Expected: `Applying migration '..._InitialCreate'. Done.`

- [ ] **Step 8: Commit**

```bash
git add src/IdentityService/ .config/
git commit -m "feat(identity): configure Duende IS, ASP.NET Identity, EF migrations"
```

---

### Task 3: User Registration Endpoint (TDD)

**Files:**
- Replace: `src/IdentityService/Endpoints/AccountEndpoints.cs` (expand the stub)

**Interfaces:**
- Consumes: `ApplicationUser` (Task 1), `UserManager<ApplicationUser>` (Task 2)
- Produces:
  - `POST /account/register` — body: `{ "userName": string, "email": string, "password": string }`
  - Success: `201 Created` with body `{ "id": string, "userName": string, "email": string }`
  - Failure: `400 Bad Request` with RFC 7807 validation problem details (key = Identity error code, value = description array)

- [ ] **Step 1: No unit tests for this endpoint — integration tests in Task 4 cover it. Proceed to implementation.**

The endpoint interacts with ASP.NET Core Identity's `UserManager`, which requires a real database to test meaningfully. Testing is deferred to Task 4 integration tests. This is the correct approach — avoid mocking UserManager.

- [ ] **Step 2: Implement registration endpoint**

Replace `src/IdentityService/Endpoints/AccountEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Identity;
using TCGTrading.IdentityService.Models;

namespace TCGTrading.IdentityService.Endpoints;

public record RegisterRequest(string UserName, string Email, string Password);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account");

        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = new ApplicationUser
            {
                UserName = request.UserName,
                Email = request.Email
            };

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
                return Results.ValidationProblem(
                    result.Errors.ToDictionary(
                        e => e.Code,
                        e => new[] { e.Description }));

            return Results.Created(
                $"/account/{user.Id}",
                new { user.Id, user.UserName, user.Email });
        });

        return app;
    }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build TCGTrading.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/IdentityService/Endpoints/AccountEndpoints.cs
git commit -m "feat(identity): add POST /account/register endpoint"
```

---

### Task 4: Test Project + Registration Integration Tests

**Files:**
- Create: `tests/IdentityService.Tests/TCGTrading.IdentityService.Tests.csproj`
- Create: `tests/IdentityService.Tests/GlobalUsings.cs`
- Create: `tests/IdentityService.Tests/test.runsettings`
- Create: `tests/IdentityService.Tests/runtimeconfig.template.json`
- Create: `tests/IdentityService.Tests/Fixtures/IdentityServiceFixture.cs`
- Create: `tests/IdentityService.Tests/AccountEndpointsTests.cs`

**Interfaces:**
- Consumes: `POST /account/register` (Task 3), `WebApplicationFactory<Program>` (Task 2), Testcontainers.PostgreSql
- Produces: integration tests for register happy path, duplicate email, weak password

- [ ] **Step 1: Create test project**

```bash
dotnet new xunit -n TCGTrading.IdentityService.Tests -o tests/IdentityService.Tests --framework net8.0
rm tests/IdentityService.Tests/UnitTest1.cs
dotnet sln TCGTrading.sln add tests/IdentityService.Tests/TCGTrading.IdentityService.Tests.csproj
```

- [ ] **Step 2: Replace the generated .csproj**

Replace `tests/IdentityService.Tests/TCGTrading.IdentityService.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RunSettingsFilePath>$(MSBuildProjectDirectory)/test.runsettings</RunSettingsFilePath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <ProjectReference Include="../../src/IdentityService/TCGTrading.IdentityService.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add GlobalUsings.cs**

Create `tests/IdentityService.Tests/GlobalUsings.cs`:
```csharp
global using Xunit;
```

- [ ] **Step 4: Add test.runsettings**

Create `tests/IdentityService.Tests/test.runsettings`:
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

- [ ] **Step 5: Add runtimeconfig.template.json**

Create `tests/IdentityService.Tests/runtimeconfig.template.json`:
```json
{
  "configProperties": {
    "System.Runtime.RollForward": "LatestMajor"
  }
}
```

- [ ] **Step 6: Write failing registration tests**

Create `tests/IdentityService.Tests/AccountEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.IdentityService.Endpoints;

namespace TCGTrading.IdentityService.Tests;

public class AccountEndpointsTests(IdentityServiceFixture fixture)
    : IClassFixture<IdentityServiceFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task Register_ValidUser_Returns201WithUserDetails()
    {
        var request = new RegisterRequest(
            UserName: "pikachu_trainer",
            Email: "ash@pokecenter.com",
            Password: "Pika@123456");

        var response = await _client.PostAsJsonAsync("/account/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisteredUserResponse>();
        body!.UserName.Should().Be("pikachu_trainer");
        body.Email.Should().Be("ash@pokecenter.com");
        body.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var request = new RegisterRequest(
            UserName: "misty_trainer",
            Email: "misty@pokecenter.com",
            Password: "Water@123456");

        await _client.PostAsJsonAsync("/account/register", request);

        var duplicate = new RegisterRequest(
            UserName: "misty_trainer2",
            Email: "misty@pokecenter.com",
            Password: "Water@123456");

        var response = await _client.PostAsJsonAsync("/account/register", duplicate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400WithDetails()
    {
        var request = new RegisterRequest(
            UserName: "brock_trainer",
            Email: "brock@pokecenter.com",
            Password: "weak");

        var response = await _client.PostAsJsonAsync("/account/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    private record RegisteredUserResponse(string Id, string UserName, string Email);
}
```

- [ ] **Step 7: Run tests — verify they fail (fixture not yet defined)**

```bash
dotnet test tests/IdentityService.Tests/ -q 2>&1 | tail -10
```
Expected: FAIL — `IdentityServiceFixture` type not found.

- [ ] **Step 8: Create the test fixture**

Create `tests/IdentityService.Tests/Fixtures/IdentityServiceFixture.cs`:
```csharp
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TCGTrading.IdentityService.Data;

namespace TCGTrading.IdentityService.Tests;

public class IdentityServiceFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("db_identity_test")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    public async Task InitializeAsync() => await _db.StartAsync();

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the production DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Register with test container connection string
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_db.GetConnectionString()));
        });
    }
}
```

- [ ] **Step 9: Run tests — verify they pass**

```bash
dotnet test tests/IdentityService.Tests/ --filter "AccountEndpointsTests" -v normal 2>&1 | tail -20
```
Expected: 3 tests pass. (Testcontainers will pull `postgres:16-alpine` on first run — allow ~30s.)

- [ ] **Step 10: Commit**

```bash
git add tests/IdentityService.Tests/
git commit -m "test(identity): add registration integration tests with Testcontainers"
```

---

### Task 5: Token Flow Integration Tests

**Files:**
- Create: `tests/IdentityService.Tests/TokenFlowTests.cs`

**Interfaces:**
- Consumes: `IdentityServiceFixture` (Task 4), `POST /account/register` (Task 3), `POST /connect/token` (Duende IS, Task 2)
- Produces: tests that verify ROPC token issuance and JWT structure (header.payload.signature format, correct `sub` and `scope` claims)

- [ ] **Step 1: Write failing token flow tests**

Create `tests/IdentityService.Tests/TokenFlowTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TCGTrading.IdentityService.Endpoints;

namespace TCGTrading.IdentityService.Tests;

public class TokenFlowTests(IdentityServiceFixture fixture)
    : IClassFixture<IdentityServiceFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task GetToken_ValidCredentials_ReturnsAccessToken()
    {
        // Register user first
        await _client.PostAsJsonAsync("/account/register", new RegisterRequest(
            UserName: "gary_oak",
            Email: "gary@pokecenter.com",
            Password: "Gary@123456"));

        // Request token via ROPC — username is the UserName field, NOT email
        var tokenResponse = await _client.PostAsync("/connect/token",
            new FormUrlEncodedContent([
                new("grant_type", "password"),
                new("client_id", "test-client"),
                new("client_secret", "test-secret"),
                new("username", "gary_oak"),
                new("password", "Gary@123456"),
                new("scope", "openid profile tcg.full"),
            ]));

        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("access_token").GetString();

        accessToken.Should().NotBeNullOrEmpty();
        // JWT has three dot-separated parts
        accessToken!.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task GetToken_WrongPassword_Returns400()
    {
        await _client.PostAsJsonAsync("/account/register", new RegisterRequest(
            UserName: "professor_oak",
            Email: "oak@pokecenter.com",
            Password: "Oak@123456"));

        var tokenResponse = await _client.PostAsync("/connect/token",
            new FormUrlEncodedContent([
                new("grant_type", "password"),
                new("client_id", "test-client"),
                new("client_secret", "test-secret"),
                new("username", "professor_oak"),
                new("password", "WrongPassword@1"),
                new("scope", "openid tcg.full"),
            ]));

        tokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("invalid_grant");
    }

    [Fact]
    public async Task DiscoveryEndpoint_Returns200WithIssuerAndJwks()
    {
        var response = await _client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("issuer").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("jwks_uri").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetToken_ValidCredentials_TokenContainsSubClaim()
    {
        await _client.PostAsJsonAsync("/account/register", new RegisterRequest(
            UserName: "nurse_joy",
            Email: "joy@pokecenter.com",
            Password: "Joy@123456"));

        var tokenResponse = await _client.PostAsync("/connect/token",
            new FormUrlEncodedContent([
                new("grant_type", "password"),
                new("client_id", "test-client"),
                new("client_secret", "test-secret"),
                new("username", "nurse_joy"),
                new("password", "Joy@123456"),
                new("scope", "openid profile tcg.full"),
            ]));

        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("access_token").GetString()!;

        // Decode JWT payload (middle segment)
        var payloadBase64 = accessToken.Split('.')[1];
        // Pad base64url to standard base64
        var padded = payloadBase64.PadRight(payloadBase64.Length + (4 - payloadBase64.Length % 4) % 4, '=')
                                   .Replace('-', '+').Replace('_', '/');
        var payloadJson = JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)));

        payloadJson.RootElement.TryGetProperty("sub", out _).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/IdentityService.Tests/ --filter "TokenFlowTests" -q 2>&1 | tail -10
```
Expected: FAIL — `TokenFlowTests` compiles but tests fail because fixture not yet returning correct responses (the token endpoint tests fail due to missing ROPC support or IS config issues).

If tests pass already: the IS config from Task 2 is working correctly — proceed to Step 3.

If tests fail with unexpected errors: check that `Config.cs` has `test-client` with `AllowedGrantTypes = GrantTypes.ResourceOwnerPassword` — see Task 2 Step 1.

- [ ] **Step 3: Run full test suite**

```bash
dotnet test tests/IdentityService.Tests/ -v normal 2>&1 | tail -20
```
Expected: All 7 tests passing (3 from AccountEndpointsTests + 4 from TokenFlowTests).

- [ ] **Step 4: Commit**

```bash
git add tests/IdentityService.Tests/TokenFlowTests.cs
git commit -m "test(identity): add token flow and discovery integration tests"
```

---

### Task 6: Dockerfile + Docker Compose

**Files:**
- Create: `src/IdentityService/Dockerfile`
- Modify: `docker-compose.yml`

**Interfaces:**
- Produces: `docker compose up -d identity-service` → service starts, discovery endpoint returns 200 at `http://localhost:5001/.well-known/openid-configuration`

- [ ] **Step 1: Create Dockerfile**

Create `src/IdentityService/Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (layer-caching)
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/IdentityService/TCGTrading.IdentityService.csproj src/IdentityService/

RUN dotnet restore src/IdentityService/TCGTrading.IdentityService.csproj

# Copy full source and publish
COPY src/SharedKernel/ src/SharedKernel/
COPY src/IdentityService/ src/IdentityService/

RUN dotnet publish src/IdentityService/TCGTrading.IdentityService.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# curl needed for Docker healthcheck — not in base image by default
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.IdentityService.dll"]
```

- [ ] **Step 2: Add identity-service to docker-compose.yml**

Add the following block to `docker-compose.yml`, inside the `services:` section, after `redis:`:
```yaml
  identity-service:
    build:
      context: .
      dockerfile: src/IdentityService/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:5001
      ConnectionStrings__IdentityDb: "Host=postgres;Port=5432;Database=db_identity;Username=tcg;Password=dev"
    ports:
      - "5001:5001"
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:5001/.well-known/openid-configuration || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
```

- [ ] **Step 3: Build and start identity-service**

```bash
docker compose build identity-service
docker compose up -d identity-service
```

Wait ~45 seconds for startup (first run pulls images, runs migrations):
```bash
docker compose ps identity-service
```
Expected: `healthy`

- [ ] **Step 4: Verify discovery endpoint**

```bash
curl -s http://localhost:5001/.well-known/openid-configuration | python3 -m json.tool | head -20
```
Expected: JSON with `issuer`, `authorization_endpoint`, `token_endpoint`, `jwks_uri` fields.

- [ ] **Step 5: Verify JWKS endpoint**

```bash
curl -s http://localhost:5001/.well-known/openid-configuration/jwks | python3 -m json.tool
```
Expected: JSON with `keys` array containing at least one key.

- [ ] **Step 6: Run full test suite to confirm no regressions**

```bash
dotnet test TCGTrading.sln --verbosity normal 2>&1 | tail -10
```
Expected: All tests passing (23 SharedKernel + 7 IdentityService.Tests = 30 total).

- [ ] **Step 7: Commit**

```bash
git add src/IdentityService/Dockerfile docker-compose.yml
git commit -m "feat(identity): add Dockerfile and Docker Compose service"
```

---

## Plan Sequence

This is Plan 2 of 7.

| Plan | Scope | Depends on |
|---|---|---|
| Plan 1 (done) | Foundation, SharedKernel, Docker Compose | — |
| **Plan 2** (this) | Identity Service — Duende IS, OAuth2/OIDC, user registration | Plan 1 |
| Plan 3 | Card Catalog Service — ITcgProvider, Pokemon TCG API | Plan 1 |
| Plan 4 | Portfolio Service — holdings, grading, manual entry, P&L | Plans 1, 3 |
| Plan 5 | Pricing Service — scheduled polling, CardPriceUpdated events | Plans 1, 3 |
| Plan 6 | Marketplace Service — listings, offers, purchases | Plans 1, 3, 4 |
| Plan 7 | Notification Service + API Gateway — SignalR, YARP JWT validation | Plans 1–6 |
