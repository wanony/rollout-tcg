using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TCGTrading.IdentityService.Endpoints;

namespace TCGTrading.IdentityService.Tests;

public class TokenFlowTests(IdentityServiceFixture fixture)
    : IClassFixture<IdentityServiceFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task GetToken_ValidCredentials_ReturnsAccessToken()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"gary_{suffix}";

        // Register user first
        await _client.PostAsJsonAsync("/account/register", new RegisterRequest(
            UserName: userName,
            Email: $"gary_{suffix}@pokecenter.com",
            Password: "Gary@123456"));

        // Request token via ROPC — username is the UserName field, NOT email
        var tokenResponse = await _client.PostAsync("/connect/token",
            new FormUrlEncodedContent([
                new("grant_type", "password"),
                new("client_id", "test-client"),
                new("client_secret", "test-secret"),
                new("username", userName),
                new("password", "Gary@123456"),
                new("scope", "openid profile tcg.full"),
            ]));

        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("access_token").GetString();

        accessToken.Should().NotBeNullOrEmpty();
        // JWT has three dot-separated parts
        accessToken!.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task GetToken_WrongPassword_Returns400()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"prof_{suffix}";

        await _client.PostAsJsonAsync("/account/register", new RegisterRequest(
            UserName: userName,
            Email: $"oak_{suffix}@pokecenter.com",
            Password: "Oak@123456"));

        var tokenResponse = await _client.PostAsync("/connect/token",
            new FormUrlEncodedContent([
                new("grant_type", "password"),
                new("client_id", "test-client"),
                new("client_secret", "test-secret"),
                new("username", userName),
                new("password", "WrongPassword@1"),
                new("scope", "openid tcg.full"),
            ]));

        tokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("invalid_grant");
    }

    [Fact]
    public async Task DiscoveryEndpoint_Returns200WithIssuerAndJwks()
    {
        var response = await _client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("issuer").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("jwks_uri").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetToken_ValidCredentials_TokenContainsSubClaim()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"joy_{suffix}";

        await _client.PostAsJsonAsync("/account/register", new RegisterRequest(
            UserName: userName,
            Email: $"joy_{suffix}@pokecenter.com",
            Password: "Joy@123456"));

        var tokenResponse = await _client.PostAsync("/connect/token",
            new FormUrlEncodedContent([
                new("grant_type", "password"),
                new("client_id", "test-client"),
                new("client_secret", "test-secret"),
                new("username", userName),
                new("password", "Joy@123456"),
                new("scope", "openid profile tcg.full"),
            ]));

        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("access_token").GetString()!;

        // Decode JWT payload (middle segment)
        var payloadBase64 = accessToken.Split('.')[1];
        // Pad base64url to standard base64
        var padded = payloadBase64.PadRight(payloadBase64.Length + (4 - payloadBase64.Length % 4) % 4, '=')
                                   .Replace('-', '+').Replace('_', '/');
        var payloadJson = JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)));

        payloadJson.RootElement.TryGetProperty("sub", out _).Should().BeTrue();
    }
}
