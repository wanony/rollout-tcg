using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Infrastructure.Persistence;
using TCGTrading.Pricing.Infrastructure.Seed;
using TCGTrading.Pricing.Infrastructure.Telemetry;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("pricing");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddSource("tcgtrading.pricing"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddSingleton<IPricingTelemetry, PricingTelemetry>();
builder.AddPriceHistory();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<PricingMigrationService>();
    await migrator.ApplyAsync();

    var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
    if (!await repo.HasAnyAsync())
        await PriceSeed.SeedAsync(repo);
}

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
