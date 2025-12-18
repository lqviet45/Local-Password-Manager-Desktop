namespace PasswordManager.Domain.Exceptions;

/// <summary>
/// Exception khi decryption thất bại.
/// </summary>
public sealed class DecryptionFailedException : DomainException
{
    public DecryptionFailedException(string message) 
        : base(message)
    {
    }

    public DecryptionFailedException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}