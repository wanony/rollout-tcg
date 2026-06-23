using Meilisearch;
using TCGTrading.CardCatalog.Application.Interfaces;

namespace TCGTrading.CardCatalog.Infrastructure.Search;

public static class SearchExtensions
{
    public static IHostApplicationBuilder AddSearch(this IHostApplicationBuilder builder)
    {
        var host = builder.Configuration["MeiliSearch:Host"]
            ?? throw new InvalidOperationException("MeiliSearch:Host is not configured.");
        var apiKey = builder.Configuration["MeiliSearch:ApiKey"] ?? "masterKey";

        builder.Services.AddSingleton(_ => new MeilisearchClient(host, apiKey));
        builder.Services.AddScoped<ICardSearchService, MeiliSearchCardSearchService>();

        return builder;
    }
}
