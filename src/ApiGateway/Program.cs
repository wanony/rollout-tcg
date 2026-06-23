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
