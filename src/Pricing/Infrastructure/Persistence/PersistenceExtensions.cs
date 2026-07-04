using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCGTrading.Pricing.Application.Interfaces;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public static class OutboxPersistenceExtensions
{
    public static IHostApplicationBuilder AddOutboxPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<PricingOutboxDbContext>(opts =>
            opts.UseNpgsql(builder.Configuration.GetConnectionString("PricingOutboxDb")));
        builder.Services.AddScoped<IWatchedCardRepository, WatchedCardRepository>();
        return builder;
    }
}
