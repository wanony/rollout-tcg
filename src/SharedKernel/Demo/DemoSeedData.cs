namespace TCGTrading.SharedKernel.Demo;

// A well-known demo account seeded by IdentityService and referenced by Portfolio/Marketplace
// when they seed sample data for it, so all three agree on the same user without a runtime call.
public static class DemoSeedData
{
    public const string UserId = "d0000000-0000-0000-0000-000000000001";
    public const string Email = "demo@rollout.dev";
    public const string Password = "Demo1234!";
}
