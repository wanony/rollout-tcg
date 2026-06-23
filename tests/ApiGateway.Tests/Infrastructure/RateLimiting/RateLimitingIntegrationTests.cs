using System.Net;
using FluentAssertions;

namespace TCGTrading.ApiGateway.Tests.Infrastructure.RateLimiting;

public class RateLimitingIntegrationTests(ApiGatewayFixture fixture)
    : IClassFixture<ApiGatewayFixture>
{
    [Fact]
    public async Task AnonymousPolicy_EnforcesIpLimit_Returns429AfterLimit()
    {
        // Uses X-Forwarded-For: 10.1.1.1 — unique key so it doesn't collide with other tests
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.1");

        // First 3 requests should succeed (limit = 3 in test fixture)
        for (var i = 0; i < 3; i++)
        {
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"request {i + 1} of 3 should be allowed");
        }

        // 4th request should be rate limited
        var limitedResponse = await client.GetAsync("/");
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task AuthenticatedPolicy_EnforcesUserLimit_Returns429AfterLimit()
    {
        // sub=user-test-2 — unique key so it doesn't collide with other tests
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateJwt("user-test-2"));

        // First 5 requests should succeed (authenticated limit = 5 in test fixture)
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"request {i + 1} of 5 should be allowed");
        }

        // 6th request should be rate limited
        var limitedResponse = await client.GetAsync("/");
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task AuthenticatedPolicy_BypassesAnonymousLimit_HigherLimitApplied()
    {
        // X-Forwarded-For: 10.1.1.3 and sub=user-test-3 — unique keys
        // Sends 4 requests: exceeds anonymous limit (3) but not authenticated limit (5)
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.3");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateJwt("user-test-3"));

        for (var i = 0; i < 4; i++)
        {
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"authenticated user should use the 5-request limit, not the 3-request limit; " +
                $"request {i + 1} should be allowed");
        }
    }

    private static string CreateJwt(string sub)
    {
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncode($"""{"sub":"{sub}"}""");
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(string input) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
