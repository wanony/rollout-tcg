using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TCGTrading.Pricing.Application.DTOs;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Tests.Fixtures;

namespace TCGTrading.Pricing.Tests.Integration;

public class PriceHistoryIntegrationTests(PricingFixture fixture)
    : IClassFixture<PricingFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task RecordPrice_ThenGetHistory_ReturnsBucketWithPostedPrice()
    {
        var cardId = Guid.NewGuid();
        const decimal postedPrice = 42.00m;

        var postResponse = await _client.PostAsJsonAsync(
            $"/prices/{cardId}",
            new RecordPriceRequest(postedPrice));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.GetAsync(
            $"/prices/{cardId}/history?granularity=day");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        body.Should().NotBeNull();
        body!.Buckets.Should().HaveCount(1);
        body.Buckets[0].Close.Should().Be(postedPrice);
    }

    [Fact]
    public async Task HourlyDataBucketedDaily_Returns_OneBucket()
    {
        var cardId = Guid.NewGuid();
        var day = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        await using var scope = fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        for (var h = 0; h < 24; h++)
        {
            await repo.AddAsync(PriceSnapshot.Create(
                cardId, 10m + h, "USD", "test", day.AddHours(h)));
        }

        var from = day.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var to   = day.AddDays(1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var response = await _client.GetAsync(
            $"/prices/{cardId}/history?granularity=day&from={from}&to={to}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        body.Should().NotBeNull();
        body!.Buckets.Should().HaveCount(1);
    }

    [Fact]
    public async Task TimeRangeFilter_Returns_OnlyBucketsInRange()
    {
        var cardId = Guid.NewGuid();
        var today  = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        await using var scope = fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        for (var i = 10; i >= 1; i--)
        {
            await repo.AddAsync(PriceSnapshot.Create(
                cardId, 10m + i, "USD", "test", today.AddDays(-i)));
        }

        var from = today.AddDays(-3).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var to   = today.AddDays(1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var response = await _client.GetAsync(
            $"/prices/{cardId}/history?granularity=day&from={from}&to={to}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        body.Should().NotBeNull();
        body!.Buckets.Should().HaveCount(3,
            "only the 3 records at days -3, -2, -1 fall within the query range");
    }
}
