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
