using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.CardCatalog.Application.DTOs;

namespace TCGTrading.CardCatalog.Tests.Integration;

public class SearchIntegrationTests(CardCatalogFixture fixture)
    : IClassFixture<CardCatalogFixture>
{
    [Fact]
    public async Task FuzzyNameSearch_Returns_MatchingCards()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/cards/search?q=drag");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "fuzzy search for 'drag' should match 'Dragon Fire' and 'Ancient Dragon'");
        var body = await response.Content.ReadFromJsonAsync<CardSearchResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        body.Items.Should().Contain(i =>
            i.Name.Contains("Dragon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FilterBySetAndRarity_Returns_SingleMatch()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync(
            "/cards/search?set=Core%20Set%202024&rarity=Rare");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CardSearchResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(1);
        body.Items[0].Name.Should().Be("Dragon Fire");
    }

    [Fact]
    public async Task EmptyQuery_Returns_AllCards()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/cards/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CardSearchResponse>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(5);
        body.Items.Should().HaveCount(5);
    }
}
