using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace TCGTrading.CardCatalog.Tests;

public class CardCatalogFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("db_cards")
        .WithUsername("tcg")
        .WithPassword("dev")
        .Build();

    private readonly IContainer _meilisearch = new ContainerBuilder()
        .WithImage("getmeili/meilisearch:v1.9")
        .WithEnvironment("MEILI_MASTER_KEY", "masterKey")
        .WithEnvironment("MEILI_NO_ANALYTICS", "true")
        .WithPortBinding(7700, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(7700))
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _meilisearch.StartAsync());
    }

    public new async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _meilisearch.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CardDb"] = _postgres.GetConnectionString(),
                ["MeiliSearch:Host"] = $"http://localhost:{_meilisearch.GetMappedPublicPort(7700)}",
                ["MeiliSearch:ApiKey"] = "masterKey",
                ["CardSeed:UseFixedData"] = "true",
            });
        });
    }
}
