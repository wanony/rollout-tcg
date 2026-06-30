using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.Portfolio.Application.DTOs;

namespace TCGTrading.Portfolio.Tests.Integration;

public class PortfolioIntegrationTests(PortfolioFixture fixture)
    : IClassFixture<PortfolioFixture>
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string CardId = "a1000000-0000-0000-0000-000000000001";

    private AddCardRequest SampleRequest() => new(
        UserId, CardId, "Dragon Fire", 1, "NearMint", 10.50m);

    [Fact]
    public async Task AddCard_Returns201_AndCanBeRetrieved()
    {
        var client = fixture.CreateClient();
        var userId = Guid.NewGuid();

        var request = new AddCardRequest(userId, CardId, "Dragon Fire", 1, "NearMint", 10.50m);
        var post = await client.PostAsJsonAsync("/portfolio/cards", request);

        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await post.Content.ReadFromJsonAsync<CollectionItemResponse>();
        item.Should().NotBeNull();
        item!.CardName.Should().Be("Dragon Fire");
        item.UserId.Should().Be(userId);

        var get = await client.GetAsync($"/portfolio/cards?userId={userId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await get.Content.ReadFromJsonAsync<CollectionItemResponse[]>();
        items.Should().HaveCount(1);
        items![0].CardId.Should().Be(CardId);
    }

    [Fact]
    public async Task Summary_ReturnsCorrectAggregates()
    {
        var client = fixture.CreateClient();
        var userId = Guid.NewGuid();
        var req = new AddCardRequest(userId, CardId, "Dragon Fire", 2, "NearMint", 5.00m);

        await client.PostAsJsonAsync("/portfolio/cards", req);

        var response = await client.GetAsync($"/portfolio/summary?userId={userId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<PortfolioSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.TotalItems.Should().Be(1);
        summary.TotalCards.Should().Be(2);
        summary.TotalAcquisitionCostUsd.Should().Be(10.00m);
    }

    [Fact]
    public async Task DeleteCard_Returns204_AndItemGone()
    {
        var client = fixture.CreateClient();
        var userId = Guid.NewGuid();

        var post = await client.PostAsJsonAsync("/portfolio/cards",
            new AddCardRequest(userId, CardId, "Dragon Fire", 1, "NearMint", 8.00m));
        var item = await post.Content.ReadFromJsonAsync<CollectionItemResponse>();

        var delete = await client.DeleteAsync($"/portfolio/cards/{item!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/portfolio/cards?userId={userId}");
        var items = await get.Content.ReadFromJsonAsync<CollectionItemResponse[]>();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCard_UnknownId_Returns404()
    {
        var client = fixture.CreateClient();
        var response = await client.DeleteAsync($"/portfolio/cards/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
