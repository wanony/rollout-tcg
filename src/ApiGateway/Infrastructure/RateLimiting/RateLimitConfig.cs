namespace TCGTrading.ApiGateway.Infrastructure.RateLimiting;

public sealed class RateLimitConfig
{
    public int AnonymousPermitLimit { get; set; } = 60;
    public int AuthenticatedPermitLimit { get; set; } = 300;
    public int WindowSeconds { get; set; } = 60;
}
