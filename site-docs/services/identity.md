# Identity Service

**Port:** 5001  
**Database:** `db_identity` (PostgreSQL)  
**Technology:** Duende IdentityServer 7 + ASP.NET Core Identity + EF Core

The Identity Service is the platform's OAuth2 / OpenID Connect authority. Every token issued by the platform comes from this service. Other services validate those tokens against the JWKS endpoint exposed at `/.well-known/openid-configuration`.

## Responsibilities

- User registration and credential management (ASP.NET Core Identity)
- OAuth2 token issuance and OIDC discovery
- Hosting the login and consent UI (Razor Pages)

## OIDC discovery

```
GET http://localhost:5001/.well-known/openid-configuration
```

Returns endpoints for token issuance, JWKS (public key), and user info.

## Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/account/register` | Register a new user |
| `GET` | `/connect/authorize` | Authorization endpoint (PKCE flow) |
| `POST` | `/connect/token` | Token endpoint |
| `GET` | `/connect/userinfo` | User info endpoint |
| `GET` | `/.well-known/openid-configuration` | OIDC discovery document |

## Configuration — clients

Clients are declared in `Config.cs` in code (not the database) for simplicity:

```csharp
// spa client — used by the React SPA
new Client
{
    ClientId = "spa",
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false,
    RedirectUris = { "http://localhost/callback", "http://localhost:3000/callback" },
    AllowedScopes = { "openid", "profile", "email", "tcg.full" },
    AllowOfflineAccess = true   // issues refresh tokens
}

// test-client — ROPC for integration tests only
new Client
{
    ClientId = "test-client",
    ClientSecrets = { new Secret("test-secret".Sha256()) },
    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
    AllowedScopes = { "openid", "profile", "email", "tcg.full" }
}
```

## Configuration — scopes

| Scope | Type | Purpose |
|---|---|---|
| `openid` | Identity resource | Minimum OIDC scope — issues `sub` claim |
| `profile` | Identity resource | User profile claims |
| `email` | Identity resource | Email claim |
| `tcg.full` | API scope | Access to all TCG platform API endpoints |

## User data model

ASP.NET Core Identity manages users with its default schema (AspNetUsers, AspNetRoles, AspNetUserClaims, etc.) backed by PostgreSQL. Passwords are hashed with PBKDF2 + HMACSHA512 (Identity v3 format).

## Signing credential

Development uses `AddDeveloperSigningCredential()`, which generates and persists a signing key to disk. This key survives container restarts if the volume is present, but regenerates if the volume is removed (all existing tokens become invalid).

For production: store an X.509 certificate in a secret manager and load it via `AddSigningCredential(certificate)`.

## Seeding

At startup, a `SeedHostedService` creates the demo account if it doesn't exist:

```
email: demo@rollout.dev
password: Demo1234!
```

This is idempotent — safe to run on every restart.

## Testing

`tests/IdentityService.Tests/` uses `Testcontainers.PostgreSql` to spin up a real Postgres container and `WebApplicationFactory<Program>` for integration tests. Tests cover registration, token issuance, and the OIDC discovery document.
