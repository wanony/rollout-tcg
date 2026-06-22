using System.Diagnostics;
using TCGTrading.Pricing.Application.Interfaces;

namespace TCGTrading.Pricing.Infrastructure.Telemetry;

public sealed class PricingTelemetry : IPricingTelemetry
{
    private static readonly ActivitySource Source = new("tcgtrading.pricing", "1.0.0");

    public IDisposable? StartPriceCalculation(string cardId, string source) =>
        Source.StartActivity("pricing.calculate")
            ?.SetTag("card.id", cardId)
            .SetTag("pricing.source", source);

    public IDisposable? StartMarketDataFetch(string provider) =>
        Source.StartActivity("pricing.market_data_fetch")
            ?.SetTag("market.provider", provider);
}
