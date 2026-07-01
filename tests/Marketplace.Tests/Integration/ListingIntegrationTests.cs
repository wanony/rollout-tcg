using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.Marketplace.Application.DTOs;

namespace TCGTrading.Marketplace.Tests.Integration;

public class ListingIntegrationTests(MarketplaceFixture fixture)
    : IClassFixture<MarketplaceFixture>
{
    private static readonly Guid SellerId = Guid.NewGuid();
    private const string CardId = "basep-1";

    private CreateListingRequest SampleListing() => new(
        SellerId, CardId, "Dragon Fire", "NearMint", 15.00m);

    [Fact]
    public async Task CreateListing_Returns201_WithActiveStatus()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/listings", SampleListing());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var listing = await response.Content.ReadFromJsonAsync<ListingResponse>();
        listing.Should().NotBeNull();
        listing!.Status.Should().Be("Active");
        listing.CardName.Should().Be("Dragon Fire");
        listing.AskingPriceUsd.Should().Be(15.00m);
    }

    [Fact]
    public async Task GetListings_ReturnsOnlyActiveListings()
    {
        var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/listings", SampleListing());

        var response = await client.GetAsync("/listings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listings = await response.Content.ReadFromJsonAsync<ListingResponse[]>();
        listings.Should().NotBeEmpty();
        listings.Should().AllSatisfy(l => l.Status.Should().Be("Active"));
    }

    [Fact]
    public async Task PurchaseListing_Returns200_AndStatusIsSold()
    {
        var client = fixture.CreateClient();
        var post = await client.PostAsJsonAsync("/listings", SampleListing());
        var created = await post.Content.ReadFromJsonAsync<ListingResponse>();

        var buyerId = Guid.NewGuid();
        var purchase = await client.PostAsJsonAsync(
            $"/listings/{created!.Id}/purchase",
            new PurchaseListingRequest(buyerId));

        purchase.StatusCode.Should().Be(HttpStatusCode.OK);
        var purchased = await purchase.Content.ReadFromJsonAsync<ListingResponse>();
        purchased!.Status.Should().Be("Sold");
        purchased.BuyerId.Should().Be(buyerId);
    }

    [Fact]
    public async Task PurchaseSoldListing_Returns409()
    {
        var client = fixture.CreateClient();
        var post = await client.PostAsJsonAsync("/listings", SampleListing());
        var created = await post.Content.ReadFromJsonAsync<ListingResponse>();

        await client.PostAsJsonAsync($"/listings/{created!.Id}/purchase",
            new PurchaseListingRequest(Guid.NewGuid()));

        // Second purchase attempt
        var second = await client.PostAsJsonAsync($"/listings/{created.Id}/purchase",
            new PurchaseListingRequest(Guid.NewGuid()));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelListing_Returns204_AndRemovedFromActive()
    {
        var client = fixture.CreateClient();
        var post = await client.PostAsJsonAsync("/listings", SampleListing());
        var created = await post.Content.ReadFromJsonAsync<ListingResponse>();

        var cancel = await client.DeleteAsync($"/listings/{created!.Id}");
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Should not appear in active listings
        var active = await client.GetFromJsonAsync<ListingResponse[]>("/listings");
        active.Should().NotContain(l => l.Id == created.Id);
    }
}
