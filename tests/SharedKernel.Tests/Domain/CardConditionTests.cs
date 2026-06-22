using FluentAssertions;
using TCGTrading.SharedKernel.Domain.ValueObjects;

namespace TCGTrading.SharedKernel.Tests.Domain;

public class CardConditionTests
{
    [Fact]
    public void GradedCondition_Display_FormatsWholeGrade()
    {
        var condition = new GradedCondition("PSA", 10m, "12345678");
        condition.Display.Should().Be("PSA 10");
    }

    [Fact]
    public void GradedCondition_Display_FormatsDecimalGrade()
    {
        var condition = new GradedCondition("BGS", 9.5m, "87654321");
        condition.Display.Should().Be("BGS 9.5");
    }

    [Fact]
    public void UngradedCondition_HoldsGrade()
    {
        var condition = new UngradedCondition(UngradedGrade.NearMint);
        condition.Grade.Should().Be(UngradedGrade.NearMint);
    }

    [Fact]
    public void GradedCondition_IsCardPhysicalCondition()
    {
        CardPhysicalCondition condition = new GradedCondition("CGC", 9m, "ABC123");
        condition.Should().BeAssignableTo<CardPhysicalCondition>();
    }

    [Fact]
    public void UngradedCondition_IsCardPhysicalCondition()
    {
        CardPhysicalCondition condition = new UngradedCondition(UngradedGrade.HeavilyPlayed);
        condition.Should().BeAssignableTo<CardPhysicalCondition>();
    }
}
