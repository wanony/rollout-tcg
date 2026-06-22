namespace TCGTrading.SharedKernel.Domain.Exceptions;

public sealed class CurrencyMismatchException()
    : InvalidOperationException("Cannot perform arithmetic on money with different currencies.");
