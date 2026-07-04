namespace TCGTrading.Pricing.Application.Interfaces;

public interface ITcgdexPriceClient
{
    Task<decimal?> GetMarketPriceUsdAsync(string providerCardId, CancellationToken ct);
}
