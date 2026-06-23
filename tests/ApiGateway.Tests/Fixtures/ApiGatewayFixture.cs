using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Redis;
using TCGTrading.ApiGateway.Infrastructure.RateLimiting;

namespace TCGTrading.ApiGateway.Tests;

public class ApiGatewayFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync() => await _redis.StartAsync();

    public new async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace production Redis with test container
            var redisDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null)
                services.Remove(redisDescriptor);

            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

            // Override rate limits to small values so tests don't send hundreds of requests
            services.Configure<RateLimitConfig>(cfg =>
            {
                cfg.AnonymousPermitLimit = 3;
                cfg.AuthenticatedPermitLimit = 5;
                cfg.WindowSeconds = 60;
            });
        });
    }
}
