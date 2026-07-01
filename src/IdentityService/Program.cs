using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.IdentityService;
using TCGTrading.IdentityService.Data;
using TCGTrading.IdentityService.Endpoints;
using TCGTrading.IdentityService.Models;
using TCGTrading.SharedKernel.Demo;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("identity-service");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

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
var clients = builder.Environment.IsProduction()
    ? Config.Clients.Where(c => c.ClientId != "test-client")
    : Config.Clients;

builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
})
.AddDeveloperSigningCredential()
.AddInMemoryIdentityResources(Config.IdentityResources)
.AddInMemoryApiScopes(Config.ApiScopes)
.AddInMemoryApiResources(Config.ApiResources)
.AddInMemoryClients(clients)
.AddAspNetIdentity<ApplicationUser>();

// AddAspNetIdentity post-configures the application cookie to SameSite=None (for cross-origin
// IdP/RP setups) but only sets Secure when the request scheme is https. Over plain http that
// combination is invalid per spec and every browser silently drops it, so login never persists
// a session. This deployment proxies everything through one origin (nginx), so Lax works fine
// and needs no TLS — registered as PostConfigure, after Duende's own, so it runs last and wins.
builder.Services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddRazorPages();
builder.Services.AddAuthorization();

// Trust X-Forwarded-Proto/Host from the Vite dev proxy (dev only)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();

// Migrate on startup — idempotent, safe in Docker restarts
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (await userManager.FindByEmailAsync(DemoSeedData.Email) is null)
    {
        var demoUser = new ApplicationUser
        {
            Id = DemoSeedData.UserId,
            UserName = DemoSeedData.Email,
            Email = DemoSeedData.Email,
            EmailConfirmed = true,
            DisplayName = "Demo"
        };
        await userManager.CreateAsync(demoUser, DemoSeedData.Password);
    }
}

app.UseForwardedHeaders();
app.UseIdentityServer();
app.UseAuthorization();

app.MapRazorPages();
app.MapAccountEndpoints();

app.Run();

// Exposes Program class to WebApplicationFactory<Program> in test projects
public partial class Program { }
