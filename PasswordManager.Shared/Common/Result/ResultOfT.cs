namespace PasswordManager.Shared.Common.Result;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// Provides functional programming patterns like Map, Bind, and Match.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public ResultError? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(ResultError error)
    {
        IsSuccess = false;
        Value = default;
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string message) => new(new ResultError(message));
    public static Result<T> Failure(string message, string? code) => new(new ResultError(message, code));
    public static Result<T> Failure(ResultError error) => new(error);
    public static Result<T> Failure(Exception exception) => new(new ResultError(exception.Message, exception.GetType().Name));

    /// <summary>
    /// Transforms the value if the result is successful, otherwise returns the error.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        return IsSuccess 
            ? Result<TOut>.Success(mapper(Value!)) 
            : Result<TOut>.Failure(Error!);
    }

    /// <summary>
    /// Transforms the value asynchronously if the result is successful, otherwise returns the error.
    /// </summary>
    public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> mapper)
    {
        return IsSuccess 
            ? Result<TOut>.Success(await mapper(Value!)) 
            : Result<TOut>.Failure(Error!);
    }

    /// <summary>
    /// Binds (flat maps) the result to another result. Useful for chaining operations that return Results.
    /// </summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
    {
        return IsSuccess 
            ? binder(Value!) 
            : Result<TOut>.Failure(Error!);
    }

    /// <summary>
    /// Binds (flat maps) the result to another result asynchronously.
    /// </summary>
    public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> binder)
    {
        return IsSuccess 
            ? await binder(Value!) 
            : Result<TOut>.Failure(Error!);
    }

    /// <summary>
    /// Matches the result to a value based on success or failure.
    /// </summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<ResultError, TOut> onFailure)
    {
        return IsSuccess 
            ? onSuccess(Value!) 
            : onFailure(Error!);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(Value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<ResultError> action)
    {
        if (IsFailure)
            action(Error!);
        return this;
    }

    /// <summary>
    /// Gets the value or throws an exception if the result is a failure.
    /// </summary>
    public T GetValueOrThrow()
    {
        if (IsFailure)
            throw new InvalidOperationException($"Cannot get value from failed result: {Error}");
        return Value!;
    }

    /// <summary>
    /// Gets the value or returns a default value if the result is a failure.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(string errorMessage) => Failure(errorMessage);
    public static implicit operator Result<T>(ResultError error) => Failure(error);
    public static implicit operator Result<T>(Exception exception) => Failure(exception);
}

