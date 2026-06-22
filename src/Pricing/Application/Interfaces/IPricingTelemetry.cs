namespace TCGTrading.Pricing.Application.Interfaces;

public interface IPricingTelemetry
{
    IDisposable? StartPriceCalculation(string cardId, string source);
    IDisposable? StartMarketDataFetch(string provider);
}
