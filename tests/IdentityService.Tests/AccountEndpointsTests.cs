using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TCGTrading.IdentityService.Endpoints;

namespace TCGTrading.IdentityService.Tests;

public class AccountEndpointsTests(IdentityServiceFixture fixture)
    : IClassFixture<IdentityServiceFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task Register_ValidUser_Returns201WithUserDetails()
    {
        var request = new RegisterRequest(
            UserName: "pikachu_trainer",
            Email: "ash@pokecenter.com",
            Password: "Pika@123456");

        var response = await _client.PostAsJsonAsync("/account/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisteredUserResponse>();
        body!.UserName.Should().Be("pikachu_trainer");
        body.Email.Should().Be("ash@pokecenter.com");
        body.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var request = new RegisterRequest(
            UserName: "misty_trainer",
            Email: "misty@pokecenter.com",
            Password: "Water@123456");

        await _client.PostAsJsonAsync("/account/register", request);

        var duplicate = new RegisterRequest(
            UserName: "misty_trainer2",
            Email: "misty@pokecenter.com",
            Password: "Water@123456");

        var response = await _client.PostAsJsonAsync("/account/register", duplicate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400WithDetails()
    {
        var request = new RegisterRequest(
            UserName: "brock_trainer",
            Email: "brock@pokecenter.com",
            Password: "weak");

        var response = await _client.PostAsJsonAsync("/account/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    private record RegisteredUserResponse(string Id, string UserName, string Email);
}
