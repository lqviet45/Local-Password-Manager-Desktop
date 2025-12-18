namespace PasswordManager.Shared.Common.Result;

/// <summary>
/// Extension methods for Result pattern operations.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result<T> to a non-generic Result, discarding the value.
    /// </summary>
    public static Result ToResult<T>(this Result<T> result)
    {
        return result.IsSuccess 
            ? Result.Success() 
            : Result.Failure(result.Error!);
    }

    /// <summary>
    /// Converts a Task<Result<T>> to Task<Result>.
    /// </summary>
    public static async Task<Result> ToResultAsync<T>(this Task<Result<T>> task)
    {
        var result = await task;
        return result.ToResult();
    }

    /// <summary>
    /// Combines multiple Results into a single Result. All must succeed for success.
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        var failures = results
            .Where(r => r.IsFailure)
            .Select(r => r.Error!)
            .ToList();

        return failures.Any()
            ? Result.Failure(new ResultError(
                $"Multiple errors occurred: {string.Join("; ", failures.Select(f => f.Message))}",
                "COMBINED_ERRORS"))
            : Result.Success();
    }

    /// <summary>
    /// Combines multiple Result<T> into a single Result. All must succeed for success.
    /// </summary>
    public static Result<IEnumerable<T>> Combine<T>(params Result<T>[] results)
    {
        var failures = results
            .Where(r => r.IsFailure)
            .Select(r => r.Error!)
            .ToList();

        if (failures.Any())
        {
            return Result<IEnumerable<T>>.Failure(new ResultError(
                $"Multiple errors occurred: {string.Join("; ", failures.Select(f => f.Message))}",
                "COMBINED_ERRORS"));
        }

        return Result<IEnumerable<T>>.Success(results.Select(r => r.Value!));
    }

    /// <summary>
    /// Executes an action and wraps the result in a Result, catching exceptions.
    /// </summary>
    public static Result<T> Try<T>(Func<T> func)
    {
        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async action and wraps the result in a Result, catching exceptions.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return Result<T>.Success(await func());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an action and wraps the result in a Result, catching exceptions.
    /// </summary>
    public static Result Try(Action action)
    {
        try
        {
            action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async action and wraps the result in a Result, catching exceptions.
    /// </summary>
    public static async Task<Result> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex);
        }
    }

    /// <summary>
    /// Converts a nullable value to a Result, treating null as failure.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, string? errorMessage = null) where T : class
    {
        return value != null 
            ? Result<T>.Success(value) 
            : Result<T>.Failure(errorMessage ?? "Value is null");
    }

    /// <summary>
    /// Converts a nullable value to a Result, treating null as failure.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, string? errorMessage = null) where T : struct
    {
        return value.HasValue 
            ? Result<T>.Success(value.Value) 
            : Result<T>.Failure(errorMessage ?? "Value is null");
    }
}

