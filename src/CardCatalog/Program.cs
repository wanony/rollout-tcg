using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.CardCatalog.Application.DTOs;
using TCGTrading.CardCatalog.Application.Interfaces;
using TCGTrading.CardCatalog.Domain.Entities;
using TCGTrading.CardCatalog.Domain.Enums;
using TCGTrading.CardCatalog.Infrastructure.Persistence;
using TCGTrading.CardCatalog.Infrastructure.Search;
using TCGTrading.CardCatalog.Infrastructure.Seed;
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

// Apply EF migrations, seed cards if empty, sync MeiliSearch index
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CardCatalogDbContext>();
    await db.Database.MigrateAsync();

    var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
    var search = scope.ServiceProvider.GetRequiredService<ICardSearchService>();

    var existing = await repo.GetAllAsync();
    if (existing.Count == 0)
    {
        foreach (var card in CardSeed.Cards)
            await repo.AddAsync(card);
        existing = await repo.GetAllAsync();
    }

    await search.IndexAllAsync(existing);
}

app.MapPrometheusScrapingEndpoint();
app.MapGet("/", () => "Hello World!");

app.MapGet("/cards/search", async (
    ICardSearchService search,
    string? q,
    string? set,
    string? rarity,
    int page = 1,
    int pageSize = 20,
    CancellationToken ct = default) =>
{
    pageSize = Math.Min(pageSize, 100);
    var result = await search.SearchAsync(q, set, rarity, page, pageSize, ct);
    return Results.Ok(result);
});

app.MapPost("/cards", async (
    CreateCardRequest request,
    ICardRepository repo,
    ICardSearchService search,
    CancellationToken ct) =>
{
    var card = Card.Create(request.Name, request.Set, request.Rarity, request.Type, request.Text);
    await repo.AddAsync(card, ct);
    await search.IndexAsync(card, ct);
    return Results.Created($"/cards/{card.Id}", card);
});

app.Run();

public partial class Program { }

public sealed record CreateCardRequest(
    string Name,
    string Set,
    Rarity Rarity,
    string Type,
    string Text);
