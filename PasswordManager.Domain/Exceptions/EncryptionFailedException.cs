namespace PasswordManager.Domain.Exceptions;

/// <summary>
/// Exception khi encryption thất bại.
/// </summary>
public sealed class EncryptionFailedException : DomainException
{
    public EncryptionFailedException(string message) 
        : base(message)
    {
    }

    public EncryptionFailedException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}