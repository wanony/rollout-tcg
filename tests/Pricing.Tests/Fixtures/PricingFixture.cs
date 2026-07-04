using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace TCGTrading.Pricing.Tests.Fixtures;

public class PricingFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _timescaledb = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:latest-pg16")
        .WithDatabase("db_pricing")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    private readonly PostgreSqlContainer _outboxDb = new PostgreSqlBuilder()
        .WithDatabase("db_pricing_outbox")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder().Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _timescaledb.StartAsync(),
            _outboxDb.StartAsync(),
            _rabbitMq.StartAsync());
    }

    public new async Task DisposeAsync()
    {
        await _timescaledb.DisposeAsync();
        await _outboxDb.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PricingDb"] = _timescaledb.GetConnectionString(),
                ["ConnectionStrings:PricingOutboxDb"] = _outboxDb.GetConnectionString(),
                ["RabbitMq:Host"] = _rabbitMq.Hostname,
                ["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
            });
        });
    }
}
