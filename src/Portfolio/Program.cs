using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Portfolio.Application.DTOs;
using TCGTrading.Portfolio.Application.Interfaces;
using TCGTrading.Portfolio.Domain.Entities;
using TCGTrading.Portfolio.Infrastructure.Persistence;
using TCGTrading.SharedKernel.Demo;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("portfolio");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
    await db.Database.MigrateAsync();

    var repo = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();
    var demoUserId = Guid.Parse(DemoSeedData.UserId);
    if ((await repo.GetByUserIdAsync(demoUserId)).Count == 0)
    {
        await repo.AddAsync(CollectionItem.Create(demoUserId, "basep-1", "Pikachu", 2, "NearMint", 8.00m));
        await repo.AddAsync(CollectionItem.Create(demoUserId, "pl4-1", "Charizard", 1, "LightlyPlayed", 45.00m));
        await repo.AddAsync(CollectionItem.Create(demoUserId, "basep-3", "Mewtwo", 1, "NearMint", 15.00m));
    }
}

app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok());

app.MapPost("/portfolio/cards", async (
    AddCardRequest request,
    ICollectionRepository repo,
    CancellationToken ct) =>
{
    try
    {
        var item = CollectionItem.Create(
            request.UserId, request.CardId, request.CardName,
            request.Quantity, request.Condition, request.AcquisitionPriceUsd);
        await repo.AddAsync(item, ct);
        return Results.Created($"/portfolio/cards/{item.Id}", item.ToResponse());
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/portfolio/cards", async (
    Guid userId,
    ICollectionRepository repo,
    CancellationToken ct) =>
{
    var items = await repo.GetByUserIdAsync(userId, ct);
    return Results.Ok(items.Select(i => i.ToResponse()));
});

app.MapGet("/portfolio/summary", async (
    Guid userId,
    ICollectionRepository repo,
    CancellationToken ct) =>
{
    var items = await repo.GetByUserIdAsync(userId, ct);
    return Results.Ok(new PortfolioSummaryResponse(
        TotalItems: items.Count,
        TotalCards: items.Sum(i => i.Quantity),
        TotalAcquisitionCostUsd: items.Sum(i => i.AcquisitionPriceUsd * i.Quantity)));
});

app.MapDelete("/portfolio/cards/{id:guid}", async (
    Guid id,
    ICollectionRepository repo,
    CancellationToken ct) =>
{
    var item = await repo.GetByIdAsync(id, ct);
    if (item is null) return Results.NotFound();
    await repo.DeleteAsync(item, ct);
    return Results.NoContent();
});

app.Run();

public partial class Program { }

static class CollectionItemExtensions
{
    public static CollectionItemResponse ToResponse(this CollectionItem item) => new(
        item.Id, item.UserId, item.CardId, item.CardName,
        item.Quantity, item.Condition, item.AcquisitionPriceUsd, item.CreatedAt);
}
