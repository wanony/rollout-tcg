using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TCGTrading.Pricing.Infrastructure.Persistence;

public sealed class PricingMigrationService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("PricingDb")
        ?? throw new InvalidOperationException("ConnectionStrings:PricingDb is not configured.");

    public async Task ApplyAsync(CancellationToken ct = default)
    {
        var assembly = typeof(PricingMigrationService).Assembly;
        const string resourceName =
            "TCGTrading.Pricing.Api.Infrastructure.Persistence.migration.sql";

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(ct);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }
}
