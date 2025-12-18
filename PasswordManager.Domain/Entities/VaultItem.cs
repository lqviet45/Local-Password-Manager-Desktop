using PasswordManager.Domain.Enums;

namespace PasswordManager.Domain.Entities;

/// <summary>
/// Represents a password vault entry (login, secure note, card, etc.).
/// Immutable record for thread safety and value semantics.
/// </summary>
public class VaultItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Item type (Login, SecureNote, CreditCard, etc.)
    /// </summary>
    public required VaultItemType Type { get; init; }
    
    /// <summary>
    /// Display name for the item
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Username/email (for Login type)
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Encrypted password or sensitive data
    /// </summary>
    public required string EncryptedData { get; init; }
    
    /// <summary>
    /// Website URL (for Login type)
    /// </summary>
    public string? Url { get; init; }
    
    /// <summary>
    /// Optional notes
    /// </summary>
    public string? Notes { get; init; }
    
    /// <summary>
    /// Tags for organization (comma-separated)
    /// </summary>
    public string? Tags { get; init; }
    
    /// <summary>
    /// Indicates if item is marked as favorite
    /// </summary>
    public bool IsFavorite { get; init; }
    
    /// <summary>
    /// Version number for conflict resolution during sync
    /// </summary>
    public long Version { get; init; }
    
    /// <summary>
    /// UTC timestamp of creation
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// UTC timestamp of last modification
    /// </summary>
    public DateTime LastModifiedUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Indicates if item has been deleted (soft delete)
    /// </summary>
    public bool IsDeleted { get; init; }
    
    /// <summary>
    /// Indicates if item needs to be synced to server (Premium users)
    /// </summary>
    public bool NeedsSync { get; init; }
    
    /// <summary>
    /// Hash of encrypted data for integrity checking
    /// </summary>
    public string? DataHash { get; init; }
}