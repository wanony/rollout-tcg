using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace TCGTrading.Pricing.Tests.Fixtures;

public class PricingFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _timescaledb = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:latest-pg16")
        .WithDatabase("db_pricing")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    public async Task InitializeAsync() => await _timescaledb.StartAsync();

    public new async Task DisposeAsync()
    {
        await _timescaledb.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PricingDb"] = _timescaledb.GetConnectionString(),
            });
        });
    }
}
