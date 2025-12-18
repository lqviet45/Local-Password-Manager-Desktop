namespace PasswordManager.Domain.Exceptions;

/// <summary>
/// Exception khi có xung đột sync giữa local và remote.
/// </summary>
public sealed class SyncConflictException : DomainException
{
    public Guid ItemId { get; }

    public SyncConflictException(Guid itemId, string message) 
        : base(message)
    {
        ItemId = itemId;
    }
}