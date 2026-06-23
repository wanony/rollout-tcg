using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.Notification.Application.DTOs;

namespace TCGTrading.Notification.Tests.Integration;

public class NotificationEndpointTests(NotificationFixture fixture)
    : IClassFixture<NotificationFixture>
{
    [Fact]
    public async Task GetNotifications_EmptyUser_ReturnsEmptyArray()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync($"/notifications?userId={Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<NotificationResponse[]>();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_UnknownId_Returns404()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsync(
            $"/notifications/{Guid.NewGuid()}/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
