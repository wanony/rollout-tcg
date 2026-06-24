using Meilisearch;
using TCGTrading.CardCatalog.Application.Interfaces;

namespace TCGTrading.CardCatalog.Infrastructure.Search;

public static class SearchExtensions
{
    public static IHostApplicationBuilder AddSearch(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var host = cfg["MeiliSearch:Host"]
                ?? throw new InvalidOperationException("MeiliSearch:Host is not configured.");
            var apiKey = cfg["MeiliSearch:ApiKey"] ?? "masterKey";
            return new MeilisearchClient(host, apiKey);
        });
        builder.Services.AddScoped<ICardSearchService, MeiliSearchCardSearchService>();

        return builder;
    }
}
