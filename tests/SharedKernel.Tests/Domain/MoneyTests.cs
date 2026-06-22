using FluentAssertions;
using TCGTrading.SharedKernel.Domain.Exceptions;
using TCGTrading.SharedKernel.Domain.ValueObjects;

namespace TCGTrading.SharedKernel.Tests.Domain;

public class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        var a = new Money(10.00m, "USD");
        var b = new Money(5.50m, "USD");
        a.Add(b).Should().Be(new Money(15.50m, "USD"));
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "GBP");
        var act = () => a.Add(b);
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Subtract_SameCurrency_ReturnsDifference()
    {
        var a = new Money(10m, "USD");
        var b = new Money(3m, "USD");
        a.Subtract(b).Should().Be(new Money(7m, "USD"));
    }

    [Fact]
    public void Subtract_DifferentCurrency_Throws()
    {
        var a = new Money(10m, "USD");
        var b = new Money(3m, "GBP");
        var act = () => a.Subtract(b);
        act.Should().Throw<CurrencyMismatchException>();
    }
}
