using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.Marketplace.Application.DTOs;

namespace TCGTrading.Marketplace.Tests.Integration;

public class OfferIntegrationTests(MarketplaceFixture fixture)
    : IClassFixture<MarketplaceFixture>
{
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid CardId = new("a1000000-0000-0000-0000-000000000001");

    private CreateListingRequest SampleListing() => new(
        SellerId, CardId, "Dragon Fire", "NearMint", 15.00m);

    private async Task<ListingResponse> CreateListingAsync(HttpClient client)
    {
        var post = await client.PostAsJsonAsync("/listings", SampleListing());
        return (await post.Content.ReadFromJsonAsync<ListingResponse>())!;
    }

    [Fact]
    public async Task MakeOffer_Returns201_WithPendingStatus()
    {
        var client = fixture.CreateClient();
        var listing = await CreateListingAsync(client);
        var buyerId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/listings/{listing.Id}/offers", new MakeOfferRequest(buyerId, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var offer = await response.Content.ReadFromJsonAsync<OfferResponse>();
        offer.Should().NotBeNull();
        offer!.Status.Should().Be("Pending");
        offer.ListingId.Should().Be(listing.Id);
        offer.OfferedPriceUsd.Should().Be(10.00m);
    }

    [Fact]
    public async Task MakeOffer_OnSoldListing_Returns409()
    {
        var client = fixture.CreateClient();
        var listing = await CreateListingAsync(client);
        await client.PostAsJsonAsync($"/listings/{listing.Id}/purchase",
            new PurchaseListingRequest(Guid.NewGuid()));

        var response = await client.PostAsJsonAsync(
            $"/listings/{listing.Id}/offers", new MakeOfferRequest(Guid.NewGuid(), 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetOffersForListing_ReturnsMadeOffer()
    {
        var client = fixture.CreateClient();
        var listing = await CreateListingAsync(client);
        await client.PostAsJsonAsync(
            $"/listings/{listing.Id}/offers", new MakeOfferRequest(Guid.NewGuid(), 12.00m));

        var response = await client.GetAsync($"/listings/{listing.Id}/offers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var offers = await response.Content.ReadFromJsonAsync<OfferResponse[]>();
        offers.Should().ContainSingle(o => o.OfferedPriceUsd == 12.00m);
    }

    [Fact]
    public async Task AcceptOffer_Returns200_MarksListingSold_AndRejectsOtherOffers()
    {
        var client = fixture.CreateClient();
        var listing = await CreateListingAsync(client);

        var winningPost = await client.PostAsJsonAsync(
            $"/listings/{listing.Id}/offers", new MakeOfferRequest(Guid.NewGuid(), 12.00m));
        var winning = await winningPost.Content.ReadFromJsonAsync<OfferResponse>();

        var losingPost = await client.PostAsJsonAsync(
            $"/listings/{listing.Id}/offers", new MakeOfferRequest(Guid.NewGuid(), 8.00m));
        var losing = await losingPost.Content.ReadFromJsonAsync<OfferResponse>();

        var accept = await client.PostAsync($"/offers/{winning!.Id}/accept", null);

        accept.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await accept.Content.ReadFromJsonAsync<OfferResponse>();
        accepted!.Status.Should().Be("Accepted");

        var listingAfter = await client.GetFromJsonAsync<ListingResponse>($"/listings/{listing.Id}");
        listingAfter!.Status.Should().Be("Sold");
        listingAfter.BuyerId.Should().Be(winning.BuyerId);

        var offersAfter = await client.GetFromJsonAsync<OfferResponse[]>($"/listings/{listing.Id}/offers");
        offersAfter.Should().Contain(o => o.Id == losing!.Id && o.Status == "Rejected");
    }

    [Fact]
    public async Task RejectOffer_Returns200_WithRejectedStatus()
    {
        var client = fixture.CreateClient();
        var listing = await CreateListingAsync(client);
        var post = await client.PostAsJsonAsync(
            $"/listings/{listing.Id}/offers", new MakeOfferRequest(Guid.NewGuid(), 9.00m));
        var offer = await post.Content.ReadFromJsonAsync<OfferResponse>();

        var reject = await client.PostAsync($"/offers/{offer!.Id}/reject", null);

        reject.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await reject.Content.ReadFromJsonAsync<OfferResponse>();
        rejected!.Status.Should().Be("Rejected");
    }
}
