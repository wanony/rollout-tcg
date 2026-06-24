using Microsoft.EntityFrameworkCore;
using TCGTrading.CardCatalog.Application.Interfaces;

namespace TCGTrading.CardCatalog.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<CardCatalogDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("CardDb")
                ?? throw new InvalidOperationException("ConnectionStrings:CardDb is not configured.")));

        builder.Services.AddScoped<ICardRepository, CardRepository>();

        return builder;
    }
}
