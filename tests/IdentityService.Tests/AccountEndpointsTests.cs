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
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"pikachu_{suffix}";
        var email = $"ash_{suffix}@pokecenter.com";

        var request = new RegisterRequest(
            UserName: userName,
            Email: email,
            Password: "Pika@123456");

        var response = await _client.PostAsJsonAsync("/account/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisteredUserResponse>();
        body!.UserName.Should().Be(userName);
        body.Email.Should().Be(email);
        body.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sharedEmail = $"misty_{suffix}@pokecenter.com";

        var request = new RegisterRequest(
            UserName: $"misty_{Guid.NewGuid().ToString("N")[..8]}",
            Email: sharedEmail,
            Password: "Water@123456");

        await _client.PostAsJsonAsync("/account/register", request);

        var duplicate = new RegisterRequest(
            UserName: $"misty_{Guid.NewGuid().ToString("N")[..8]}",
            Email: sharedEmail,
            Password: "Water@123456");

        var response = await _client.PostAsJsonAsync("/account/register", duplicate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400WithDetails()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var request = new RegisterRequest(
            UserName: $"brock_{suffix}",
            Email: $"brock_{suffix}@pokecenter.com",
            Password: "weak");

        var response = await _client.PostAsJsonAsync("/account/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    private record RegisteredUserResponse(string Id, string UserName, string Email);
}
