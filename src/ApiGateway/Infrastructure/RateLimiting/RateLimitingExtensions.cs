using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RedisRateLimiting;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace TCGTrading.ApiGateway.Infrastructure.RateLimiting;

public static class RateLimitingExtensions
{
    public static IHostApplicationBuilder AddRateLimiting(
        this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration["Redis:ConnectionString"]
            ?? "localhost:6379";

        builder.Services.Configure<RateLimitConfig>(
            builder.Configuration.GetSection("RateLimit"));

        builder.Services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    var cfg = context.HttpContext.RequestServices
                        .GetRequiredService<IOptions<RateLimitConfig>>().Value;
                    context.HttpContext.Response.Headers.RetryAfter =
                        cfg.WindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsync(
                    "Rate limit exceeded.", cancellationToken);
            };

            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var cfg = httpContext.RequestServices
                        .GetRequiredService<IOptions<RateLimitConfig>>().Value;
                    var redis = httpContext.RequestServices
                        .GetRequiredService<IConnectionMultiplexer>();

                    var sub = ExtractSub(httpContext);
                    if (sub is not null)
                    {
                        return RedisRateLimitPartition.GetSlidingWindowRateLimiter(
                            partitionKey: $"rl:auth:{sub}",
                            factory: _ => new RedisSlidingWindowRateLimiterOptions
                            {
                                ConnectionMultiplexerFactory = () => redis,
                                PermitLimit = cfg.AuthenticatedPermitLimit,
                                Window = TimeSpan.FromSeconds(cfg.WindowSeconds),
                            });
                    }

                    // TODO: trust X-Forwarded-For only from known proxies via UseForwardedHeaders
                    // — as-is, any client can spoof this header to bypass IP rate limiting.
                    var ip = httpContext.Request.Headers["X-Forwarded-For"]
                                 .FirstOrDefault()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString()
                             ?? "unknown";

                    return RedisRateLimitPartition.GetSlidingWindowRateLimiter(
                        partitionKey: $"rl:anon:{ip}",
                        factory: _ => new RedisSlidingWindowRateLimiterOptions
                        {
                            ConnectionMultiplexerFactory = () => redis,
                            PermitLimit = cfg.AnonymousPermitLimit,
                            Window = TimeSpan.FromSeconds(cfg.WindowSeconds),
                        });
                });
        });

        return builder;
    }

    // TODO: when real JWT auth lands, replace manual decode with httpContext.User.FindFirst("sub")
    // — until then, sub is unverified and any caller can spoof it to get the higher auth limit.
    private static string? ExtractSub(HttpContext httpContext)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..];
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            var payload = parts[1];
            var remainder = payload.Length % 4;
            string padded;
            if (remainder == 2)
                padded = payload + "==";
            else if (remainder == 3)
                padded = payload + "=";
            else
                padded = payload;

            var base64 = padded.Replace('-', '+').Replace('_', '/');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var sub)
                ? sub.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
