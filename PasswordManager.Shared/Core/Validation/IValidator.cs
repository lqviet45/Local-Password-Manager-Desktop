using FluentValidation.Results;

namespace PasswordManager.Shared.Core.Validation;

/// <summary>
/// Abstraction for FluentValidation validators.
/// Provides a common interface for validation operations.
/// </summary>
/// <typeparam name="T">The type to validate</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Validates an instance asynchronously.
    /// </summary>
    /// <param name="instance">The instance to validate</param>
    /// <returns>A ValidationResult containing validation errors if any</returns>
    Task<ValidationResult> ValidateAsync(T instance);
}

