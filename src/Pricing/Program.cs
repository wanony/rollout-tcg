using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Pricing.Application.Interfaces;
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

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();
