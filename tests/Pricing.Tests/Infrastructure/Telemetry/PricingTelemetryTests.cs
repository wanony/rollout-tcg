using System.Diagnostics;
using FluentAssertions;
using TCGTrading.Pricing.Infrastructure.Telemetry;

namespace TCGTrading.Pricing.Tests.Infrastructure.Telemetry;

public class PricingTelemetryTests : IDisposable
{
    private readonly ActivityListener _listener;
    private Activity? _lastStarted;

    public PricingTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "tcgtrading.pricing",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStarted = a => _lastStarted = a
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void StartPriceCalculation_EmitsActivity_WithCardIdTag()
    {
        var telemetry = new PricingTelemetry();

        using var activity = telemetry.StartPriceCalculation("card-123", "market");

        _lastStarted.Should().NotBeNull();
        _lastStarted!.OperationName.Should().Be("pricing.calculate");
        _lastStarted.GetTagItem("card.id").Should().Be("card-123");
        _lastStarted.GetTagItem("pricing.source").Should().Be("market");
    }

    [Fact]
    public void StartMarketDataFetch_EmitsActivity_WithProviderTag()
    {
        var telemetry = new PricingTelemetry();

        using var activity = telemetry.StartMarketDataFetch("tcgplayer");

        _lastStarted.Should().NotBeNull();
        _lastStarted!.OperationName.Should().Be("pricing.market_data_fetch");
        _lastStarted.GetTagItem("market.provider").Should().Be("tcgplayer");
    }

    [Fact]
    public void StartPriceCalculation_ReturnsNull_WhenNoListenerRegistered()
    {
        _listener.Dispose();
        var telemetry = new PricingTelemetry();

        var result = telemetry.StartPriceCalculation("card-123", "market");

        result.Should().BeNull();
    }

    public void Dispose() => _listener.Dispose();
}
