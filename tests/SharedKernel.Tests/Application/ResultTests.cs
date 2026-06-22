using FluentAssertions;
using TCGTrading.SharedKernel.Application;

namespace TCGTrading.SharedKernel.Tests.Application;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess_True_And_Value_Accessible()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_IsSuccess_False_And_Error_Accessible()
    {
        var result = Result<int>.Failure("something went wrong");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var result = Result<int>.Failure("error");
        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Success_AccessingError_Throws()
    {
        var result = Result<int>.Success(1);
        var act = () => _ = result.Error;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsFailure_IsOppositeOfIsSuccess()
    {
        Result<string>.Success("x").IsFailure.Should().BeFalse();
        Result<string>.Failure("e").IsFailure.Should().BeTrue();
    }
}
