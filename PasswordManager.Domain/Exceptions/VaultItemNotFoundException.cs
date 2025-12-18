namespace PasswordManager.Domain.Exceptions;

/// <summary>
/// Exception khi không tìm thấy VaultItem.
/// </summary>
public sealed class VaultItemNotFoundException : DomainException
{
    public Guid ItemId { get; }

    public VaultItemNotFoundException(Guid itemId)
        : base($"Vault item with ID '{itemId}' was not found")
    {
        ItemId = itemId;
    }
}