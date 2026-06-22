namespace TCGTrading.SharedKernel.Application;

public sealed class Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
    }

    private Result(string error)
    {
        IsSuccess = false;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    public string Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error of a successful result.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}
