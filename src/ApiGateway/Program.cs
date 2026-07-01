using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress = builder.Configuration["Jwt:MetadataAddress"]
            ?? "http://identity-service:5001/.well-known/openid-configuration";
        options.RequireHttpsMetadata = false;
        // Keep the token's own claim names (e.g. "sub") — the default inbound mapping
        // rewrites them to long ClaimTypes.* URIs, which ExtractSub below doesn't expect.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuers = builder.Configuration.GetSection("Jwt:ValidIssuers").Get<string[]>(),
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "tcg-api",
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser()));

var app = builder.Build();

app.UseCors();
// Authentication before rate limiting so the limiter can partition on a verified sub claim
// instead of trusting an unverified one out of the token.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok());
app.MapReverseProxy();

app.Run();

public partial class Program { }
