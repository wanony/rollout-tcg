using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TCGTrading.Pricing.Application.Interfaces;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public static class PriceHistoryExtensions
{
    public static IHostApplicationBuilder AddPriceHistory(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IDbConnection>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("PricingDb")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:PricingDb is not configured.");
            return new NpgsqlConnection(connectionString);
        });

        builder.Services.AddScoped<IPriceRepository, TimescaleDbPriceRepository>();
        builder.Services.AddScoped<PricingMigrationService>();

        return builder;
    }
}
