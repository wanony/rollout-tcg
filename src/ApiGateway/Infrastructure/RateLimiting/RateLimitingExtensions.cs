using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RedisRateLimiting;
using StackExchange.Redis;
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

    // JWT Bearer middleware (which runs before UseRateLimiter) has already verified the token's
    // signature/issuer/audience by this point, so the sub claim here is trustworthy.
    private static string? ExtractSub(HttpContext httpContext) =>
        httpContext.User.FindFirst("sub")?.Value;
}
