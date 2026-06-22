using TCGTrading.SharedKernel.Domain.Exceptions;

namespace TCGTrading.SharedKernel.Domain.ValueObjects;

public record Money(decimal Amount, string Currency = "USD")
{
    public Money Add(Money other)
    {
        if (Currency != other.Currency) throw new CurrencyMismatchException();
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency) throw new CurrencyMismatchException();
        return this with { Amount = Amount - other.Amount };
    }
}
