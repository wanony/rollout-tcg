using FluentAssertions;
using TCGTrading.Pricing.Infrastructure.Ingestion;

namespace TCGTrading.Pricing.Tests;

public class DeterministicGuidTests
{
    [Fact]
    public void From_SameInput_ReturnsSameGuid()
    {
        var first = DeterministicGuid.From("base1-001");
        var second = DeterministicGuid.From("base1-001");

        first.Should().Be(second);
    }

    [Fact]
    public void From_DifferentInputs_ReturnsDistinctGuids()
    {
        var a = DeterministicGuid.From("base1-001");
        var b = DeterministicGuid.From("base1-002");

        a.Should().NotBe(b);
    }
}
