using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TCGTrading.Pricing.Application.Interfaces;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public static class PriceHistoryExtensions
{
    public static IHostApplicationBuilder AddPriceHistory(
        this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("PricingDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PricingDb is not configured.");

        builder.Services.AddScoped<IDbConnection>(
            _ => new NpgsqlConnection(connectionString));

        builder.Services.AddScoped<IPriceRepository, TimescaleDbPriceRepository>();
        builder.Services.AddScoped<PricingMigrationService>();

        return builder;
    }
}
