namespace PasswordManager.Domain.Exceptions;

/// <summary>
/// Base exception cho tất cả domain-level exceptions.
/// Domain exceptions thể hiện business rule violations.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}