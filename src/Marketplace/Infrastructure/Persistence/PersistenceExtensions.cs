using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCGTrading.Marketplace.Application.Interfaces;

namespace TCGTrading.Marketplace.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<MarketplaceDbContext>(opts =>
            opts.UseNpgsql(builder.Configuration.GetConnectionString("MarketplaceDb")));
        builder.Services.AddScoped<IListingRepository, ListingRepository>();
        return builder;
    }
}
