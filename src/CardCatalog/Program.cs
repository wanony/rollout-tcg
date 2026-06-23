using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.CardCatalog.Infrastructure.Persistence;
using TCGTrading.CardCatalog.Infrastructure.Search;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("card-catalog");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();
builder.AddSearch();

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
