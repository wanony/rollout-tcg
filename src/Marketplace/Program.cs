using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Marketplace.Application.DTOs;
using TCGTrading.Marketplace.Application.Interfaces;
using TCGTrading.Marketplace.Domain.Entities;
using TCGTrading.Marketplace.Infrastructure.Messaging;
using TCGTrading.Marketplace.Infrastructure.Persistence;
using TCGTrading.SharedKernel.Contracts;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("marketplace");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();
builder.AddMessaging();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
    await db.Database.MigrateAsync();
}

app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok());

app.MapPost("/listings", async (
    CreateListingRequest request,
    IListingRepository repo,
    IPublishEndpoint publish,
    CancellationToken ct) =>
{
    var listing = Listing.Create(
        request.SellerId, request.CardId, request.CardName,
        request.Condition, request.AskingPriceUsd);
    await repo.AddAsync(listing, ct);

    await publish.Publish(new ListingCreatedEvent(
        Guid.NewGuid(), DateTime.UtcNow,
        listing.Id, listing.CardId, listing.CardName,
        listing.SellerId, listing.AskingPriceUsd, listing.Condition), ct);

    return Results.Created($"/listings/{listing.Id}", listing.ToResponse());
});

app.MapGet("/listings", async (
    IListingRepository repo,
    CancellationToken ct) =>
{
    var listings = await repo.GetActiveAsync(ct);
    return Results.Ok(listings.Select(l => l.ToResponse()));
});

app.MapGet("/listings/{id:guid}", async (
    Guid id,
    IListingRepository repo,
    CancellationToken ct) =>
{
    var listing = await repo.GetByIdAsync(id, ct);
    return listing is null ? Results.NotFound() : Results.Ok(listing.ToResponse());
});

app.MapDelete("/listings/{id:guid}", async (
    Guid id,
    IListingRepository repo,
    CancellationToken ct) =>
{
    var listing = await repo.GetByIdAsync(id, ct);
    if (listing is null) return Results.NotFound();
    try { listing.Cancel(); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    await repo.UpdateAsync(listing, ct);
    return Results.NoContent();
});

app.MapPost("/listings/{id:guid}/purchase", async (
    Guid id,
    PurchaseListingRequest request,
    IListingRepository repo,
    IPublishEndpoint publish,
    CancellationToken ct) =>
{
    var listing = await repo.GetByIdAsync(id, ct);
    if (listing is null) return Results.NotFound();
    try { listing.Purchase(request.BuyerId); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    await repo.UpdateAsync(listing, ct);

    await publish.Publish(new ListingPurchasedEvent(
        Guid.NewGuid(), DateTime.UtcNow,
        listing.Id, listing.CardId,
        request.BuyerId, listing.SellerId, listing.AskingPriceUsd), ct);

    return Results.Ok(listing.ToResponse());
});

app.Run();

public partial class Program { }

static class ListingExtensions
{
    public static ListingResponse ToResponse(this Listing l) => new(
        l.Id, l.SellerId, l.CardId, l.CardName, l.Condition,
        l.AskingPriceUsd, l.Status.ToString(), l.BuyerId, l.PurchasedAt, l.CreatedAt);
}
