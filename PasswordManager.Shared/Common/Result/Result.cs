namespace PasswordManager.Shared.Common.Result;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// Non-generic version for operations that don't return a value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public ResultError? Error { get; }

    protected Result(bool isSuccess, ResultError? error)
    {
        if (isSuccess && error != null)
            throw new InvalidOperationException("A successful result cannot have an error.");
        if (!isSuccess && error == null)
            throw new InvalidOperationException("A failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string message) => new(false, new ResultError(message));
    public static Result Failure(string message, string? code) => new(false, new ResultError(message, code));
    public static Result Failure(ResultError error) => new(false, error);
    public static Result Failure(Exception exception) => new(false, new ResultError(exception.Message, exception.GetType().Name));

    public static implicit operator Result(bool success) => success ? Success() : Failure("Operation failed");
    public static implicit operator Result(string errorMessage) => Failure(errorMessage);
    public static implicit operator Result(ResultError error) => Failure(error);

    public Result<T> ToResult<T>(T value) => IsSuccess 
        ? Result<T>.Success(value) 
        : Result<T>.Failure(Error!);

    public Result<T> ToResult<T>() => IsFailure 
        ? Result<T>.Failure(Error!) 
        : throw new InvalidOperationException("Cannot convert successful Result to Result<T> without a value.");
}

