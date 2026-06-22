using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

namespace TCGTrading.SharedKernel.Infrastructure.Telemetry;

public static class TelemetryExtensions
{
    public static IHostApplicationBuilder AddTelemetry(
        this IHostApplicationBuilder builder, string serviceName)
    {
        var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var lokiEndpoint = builder.Configuration["Telemetry:LokiEndpoint"] ?? "http://localhost:3100";

        builder.Services.AddSerilog((_, lc) => lc
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.GrafanaLoki(
                lokiEndpoint,
                labels: new[]
                {
                    new LokiLabel { Key = "service", Value = serviceName },
                    new LokiLabel { Key = "app", Value = "tcgtrading" }
                }));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: "1.0.0"))
            .WithTracing(t => t
                .AddSource(serviceName)
                .AddSource("MassTransit")
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(m => m
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return builder;
    }
}
