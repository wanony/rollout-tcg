using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Alerts.Application.DTOs;
using TCGTrading.Alerts.Application.Interfaces;
using TCGTrading.Alerts.Domain.Entities;
using TCGTrading.Alerts.Domain.Enums;
using TCGTrading.Alerts.Infrastructure.Messaging;
using TCGTrading.Alerts.Infrastructure.Persistence;
using TCGTrading.SharedKernel.Contracts;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("alerts");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();
builder.AddMessaging();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AlertsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok());

app.MapPost("/alerts", async (
    CreateAlertRequest request,
    IAlertRepository repo,
    IPublishEndpoint publisher,
    CancellationToken ct) =>
{
    if (!Enum.TryParse<AlertDirection>(request.Direction, ignoreCase: true, out var direction))
    {
        return Results.BadRequest(new { error = $"Invalid direction: {request.Direction}" });
    }

    try
    {
        var alert = PriceAlert.Create(
            request.UserId, request.ProviderCardId, request.CardName,
            direction, request.TargetPriceUsd, request.Recurring);
        await repo.AddAsync(alert, ct);
        await publisher.Publish(new PriceAlertCreatedEvent(
            Guid.NewGuid(), DateTime.UtcNow, alert.Id, alert.ProviderCardId), ct);
        return Results.Created($"/alerts/{alert.Id}", alert.ToResponse());
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/alerts", async (
    Guid userId,
    IAlertRepository repo,
    CancellationToken ct) =>
{
    var items = await repo.GetByUserIdAsync(userId, ct);
    return Results.Ok(items.Select(a => a.ToResponse()));
});

app.MapDelete("/alerts/{id:guid}", async (
    Guid id,
    IAlertRepository repo,
    IPublishEndpoint publisher,
    CancellationToken ct) =>
{
    var alert = await repo.GetByIdAsync(id, ct);
    if (alert is null) return Results.NotFound();
    await repo.DeleteAsync(alert, ct);
    await publisher.Publish(new PriceAlertDeletedEvent(
        Guid.NewGuid(), DateTime.UtcNow, alert.Id, alert.ProviderCardId), ct);
    return Results.NoContent();
});

app.Run();

public partial class Program { }

static class PriceAlertExtensions
{
    public static AlertResponse ToResponse(this PriceAlert alert) => new(
        alert.Id, alert.UserId, alert.ProviderCardId, alert.CardName,
        alert.Direction.ToString(), alert.TargetPriceUsd, alert.Recurring,
        alert.IsActive, alert.LastFiredAt, alert.CreatedAt);
}
