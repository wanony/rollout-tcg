using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.Notification.Application.DTOs;
using TCGTrading.Notification.Application.Interfaces;
using TCGTrading.Notification.Domain.Entities;
using TCGTrading.Notification.Infrastructure.Hubs;
using TCGTrading.Notification.Infrastructure.Messaging;
using TCGTrading.Notification.Infrastructure.Persistence;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("notification");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddPersistence();
builder.AddMessaging();

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, UserIdProvider>();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials())); // Required for SignalR

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok());

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/notifications", async (
    Guid userId,
    INotificationRepository repo,
    CancellationToken ct) =>
{
    var items = await repo.GetByUserIdAsync(userId, ct);
    return Results.Ok(items.Select(n => n.ToResponse()));
});

app.MapPost("/notifications/{id:guid}/read", async (
    Guid id,
    INotificationRepository repo,
    CancellationToken ct) =>
{
    var notif = await repo.GetByIdAsync(id, ct);
    if (notif is null) return Results.NotFound();
    notif.MarkRead();
    await repo.UpdateAsync(notif, ct);
    return Results.Ok(notif.ToResponse());
});

app.Run();

public partial class Program { }

static class NotificationExtensions
{
    public static NotificationResponse ToResponse(this UserNotification n) => new(
        n.Id, n.UserId, n.Title, n.Message,
        n.Type.ToString(), n.ReferenceId, n.IsRead, n.CreatedAt);
}
