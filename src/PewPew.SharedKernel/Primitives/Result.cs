namespace PewPew.SharedKernel.Primitives;

public sealed class Result
{
    private Result(bool isSuccess, DomainError error)
    {
        if (isSuccess == (error != DomainError.None))
        {
            throw new ArgumentException("A success result has no error and a failure result requires one.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DomainError Error { get; }

    public static Result Success() => new(true, DomainError.None);

    public static Result Failure(DomainError error) => new(false, error ?? throw new ArgumentNullException(nameof(error)));

    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(DomainError error) => new(error);
}

public sealed class Result<T>
{
    private readonly T? value;

    internal Result(T value)
    {
        this.value = value;
        IsSuccess = true;
        Error = DomainError.None;
    }

    internal Result(DomainError error)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
        if (error == DomainError.None)
        {
            throw new ArgumentException("A failure result requires an error.", nameof(error));
        }
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DomainError Error { get; }

    public T Value => IsSuccess ? value! : throw new InvalidOperationException("A failed result has no value.");

}
