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
