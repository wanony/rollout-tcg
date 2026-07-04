using System.Net;
using FluentAssertions;
using TCGTrading.Pricing.Infrastructure.External;
using Xunit;

namespace TCGTrading.Pricing.Tests;

public class TcgdexPriceClientTests
{
    private static TcgdexPriceClient CreateClient(string jsonBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(jsonBody, statusCode);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.tcgdex.net/") };
        return new TcgdexPriceClient(httpClient);
    }

    [Fact]
    public async Task GetMarketPriceUsdAsync_ReturnsHolofoilMarketPrice_WhenPresent()
    {
        const string json = """
        {
            "id": "swsh3-136",
            "pricing": {
                "tcgplayer": {
                    "unit": "USD",
                    "holofoil": { "lowPrice": 5.0, "midPrice": 10.0, "highPrice": 20.0, "marketPrice": 12.34 }
                }
            }
        }
        """;
        var client = CreateClient(json);

        var result = await client.GetMarketPriceUsdAsync("swsh3-136", CancellationToken.None);

        result.Should().Be(12.34m);
    }

    [Fact]
    public async Task GetMarketPriceUsdAsync_ReturnsNull_WhenNoPricing()
    {
        const string json = """
        {
            "id": "swsh3-136"
        }
        """;
        var client = CreateClient(json);

        var result = await client.GetMarketPriceUsdAsync("swsh3-136", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMarketPriceUsdAsync_FallsBackToNormalVariant_WhenOnlyNormalPresent()
    {
        const string json = """
        {
            "id": "swsh3-136",
            "pricing": {
                "tcgplayer": {
                    "unit": "USD",
                    "normal": { "lowPrice": 1.0, "midPrice": 2.0, "highPrice": 3.0, "marketPrice": 2.50 }
                }
            }
        }
        """;
        var client = CreateClient(json);

        var result = await client.GetMarketPriceUsdAsync("swsh3-136", CancellationToken.None);

        result.Should().Be(2.50m);
    }

    [Fact]
    public async Task GetMarketPriceUsdAsync_ReturnsNull_WhenTcgplayerHasNoRecognizedVariant()
    {
        const string json = """
        {
            "id": "swsh3-136",
            "pricing": {
                "tcgplayer": {
                    "unit": "USD"
                }
            }
        }
        """;
        var client = CreateClient(json);

        var result = await client.GetMarketPriceUsdAsync("swsh3-136", CancellationToken.None);

        result.Should().BeNull();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string body, HttpStatusCode statusCode)
        {
            _body = body;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
