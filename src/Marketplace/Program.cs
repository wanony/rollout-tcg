using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Marketplace.Application.DTOs;
using TCGTrading.Marketplace.Application.Interfaces;
using TCGTrading.Marketplace.Domain.Entities;
using TCGTrading.Marketplace.Domain.Enums;
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

app.MapPost("/listings/{id:guid}/offers", async (
    Guid id,
    MakeOfferRequest request,
    IListingRepository listings,
    IOfferRepository offers,
    IPublishEndpoint publish,
    CancellationToken ct) =>
{
    var listing = await listings.GetByIdAsync(id, ct);
    if (listing is null) return Results.NotFound();
    if (listing.Status != ListingStatus.Active)
        return Results.Conflict($"Listing is {listing.Status}, cannot make an offer.");

    var offer = Offer.Create(id, request.BuyerId, request.OfferedPriceUsd);
    await offers.AddAsync(offer, ct);

    await publish.Publish(new OfferMadeEvent(
        Guid.NewGuid(), DateTime.UtcNow,
        offer.Id, listing.Id, listing.CardId, offer.BuyerId, listing.SellerId, offer.OfferedPriceUsd), ct);

    return Results.Created($"/offers/{offer.Id}", offer.ToResponse());
});

app.MapGet("/listings/{id:guid}/offers", async (
    Guid id,
    IOfferRepository offers,
    CancellationToken ct) =>
{
    var result = await offers.GetByListingIdAsync(id, ct);
    return Results.Ok(result.Select(o => o.ToResponse()));
});

app.MapPost("/offers/{id:guid}/accept", async (
    Guid id,
    IOfferRepository offers,
    IListingRepository listings,
    IPublishEndpoint publish,
    CancellationToken ct) =>
{
    var offer = await offers.GetByIdAsync(id, ct);
    if (offer is null) return Results.NotFound();
    var listing = await listings.GetByIdAsync(offer.ListingId, ct);
    if (listing is null) return Results.NotFound();

    try
    {
        offer.Accept();
        listing.Purchase(offer.BuyerId);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }

    await offers.UpdateAsync(offer, ct);
    await listings.UpdateAsync(listing, ct);

    foreach (var other in await offers.GetByListingIdAsync(listing.Id, ct))
    {
        if (other.Id == offer.Id || other.Status != OfferStatus.Pending) continue;
        other.Reject();
        await offers.UpdateAsync(other, ct);
    }

    await publish.Publish(new OfferAcceptedEvent(
        Guid.NewGuid(), DateTime.UtcNow,
        offer.Id, listing.Id, listing.CardId, offer.BuyerId, listing.SellerId, offer.OfferedPriceUsd), ct);
    await publish.Publish(new ListingPurchasedEvent(
        Guid.NewGuid(), DateTime.UtcNow,
        listing.Id, listing.CardId, offer.BuyerId, listing.SellerId, offer.OfferedPriceUsd), ct);

    return Results.Ok(offer.ToResponse());
});

app.MapPost("/offers/{id:guid}/reject", async (
    Guid id,
    IOfferRepository offers,
    CancellationToken ct) =>
{
    var offer = await offers.GetByIdAsync(id, ct);
    if (offer is null) return Results.NotFound();
    try { offer.Reject(); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    await offers.UpdateAsync(offer, ct);
    return Results.Ok(offer.ToResponse());
});

app.Run();

public partial class Program { }

static class ListingExtensions
{
    public static ListingResponse ToResponse(this Listing l) => new(
        l.Id, l.SellerId, l.CardId, l.CardName, l.Condition,
        l.AskingPriceUsd, l.Status.ToString(), l.BuyerId, l.PurchasedAt, l.CreatedAt);
}

static class OfferExtensions
{
    public static OfferResponse ToResponse(this Offer o) => new(
        o.Id, o.ListingId, o.BuyerId, o.OfferedPriceUsd, o.Status.ToString(), o.CreatedAt, o.RespondedAt);
}
