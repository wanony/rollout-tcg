using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace TCGTrading.Marketplace.Tests;

public class MarketplaceFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string RabbitMqUsername = "tcg";
    private const string RabbitMqPassword = "dev";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("db_marketplace")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithUsername(RabbitMqUsername)
        .WithPassword(RabbitMqPassword)
        .Build();

    public async Task InitializeAsync()
        => await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

    public new async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitmq.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MarketplaceDb"] = _postgres.GetConnectionString(),
                ["RabbitMq:Host"] = _rabbitmq.Hostname,
                ["RabbitMq:Port"] = _rabbitmq.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:Username"] = RabbitMqUsername,
                ["RabbitMq:Password"] = RabbitMqPassword
            }));
    }
}
