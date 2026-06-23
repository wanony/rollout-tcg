using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCGTrading.Portfolio.Application.Interfaces;

namespace TCGTrading.Portfolio.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<PortfolioDbContext>(opts =>
            opts.UseNpgsql(builder.Configuration.GetConnectionString("PortfolioDb")));
        builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
        return builder;
    }
}
