using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("api-gateway");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.Run();
