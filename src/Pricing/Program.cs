using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Pricing.Application.DTOs;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Domain.Enums;
using TCGTrading.Pricing.Infrastructure.Persistence;
using TCGTrading.Pricing.Infrastructure.Seed;
using TCGTrading.Pricing.Infrastructure.Telemetry;

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

app.MapPost("/prices/{cardId:guid}", async (
    Guid cardId,
    RecordPriceRequest request,
    IPriceRepository repo,
    CancellationToken ct) =>
{
    if (request.Price <= 0)
        return Results.BadRequest("Price must be greater than 0.");

    var currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency;
    var source   = string.IsNullOrWhiteSpace(request.Source)   ? "manual" : request.Source;

    var snapshot = PriceSnapshot.Create(
        cardId, request.Price, currency, source, DateTimeOffset.UtcNow);
    await repo.AddAsync(snapshot, ct);

    return Results.Created(
        $"/prices/{cardId}/history",
        new { cardId = snapshot.CardId, price = snapshot.Price, currency = snapshot.Currency, recordedAt = snapshot.RecordedAt });
});

app.MapGet("/prices/{cardId:guid}/history", async (
    Guid cardId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    string? granularity,
    IPriceRepository repo,
    CancellationToken ct) =>
{
    var gran = granularity?.ToLowerInvariant() switch
    {
        null   => Granularity.Day,
        "hour" => Granularity.Hour,
        "day"  => Granularity.Day,
        "week" => Granularity.Week,
        _      => (Granularity?)null,
    };

    if (gran is null)
        return Results.BadRequest("granularity must be 'hour', 'day', or 'week'.");

    var toTime   = to   ?? DateTimeOffset.UtcNow;
    var fromTime = from ?? toTime.AddDays(-30);

    var buckets = await repo.GetHistoryAsync(cardId, fromTime, toTime, gran.Value, ct);

    return Results.Ok(new PriceHistoryResponse(
        cardId,
        gran.Value.ToString().ToLowerInvariant(),
        fromTime,
        toTime,
        buckets));
});

app.Run();

public partial class Program { }
