using Microsoft.EntityFrameworkCore;
using TCGTrading.CardCatalog.Application.Interfaces;

namespace TCGTrading.CardCatalog.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("CardDb")
            ?? throw new InvalidOperationException("ConnectionStrings:CardDb is not configured.");

        builder.Services.AddDbContext<CardCatalogDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<ICardRepository, CardRepository>();

        return builder;
    }
}
