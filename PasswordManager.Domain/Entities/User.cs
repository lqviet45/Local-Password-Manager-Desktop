namespace PasswordManager.Domain.Entities;

/// <summary>
/// Represents an application user with authentication and subscription details.
/// </summary>
public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// User's email address (used for login)
    /// </summary>
    public required string Email { get; init; }
    
    /// <summary>
    /// Hashed master password (Argon2id)
    /// Never stored in plain text
    /// </summary>
    public required string MasterPasswordHash { get; init; }
    
    /// <summary>
    /// Salt used for master password hashing
    /// </summary>
    public required byte[] Salt { get; init; }
    
    /// <summary>
    /// Encrypted master key (used for vault encryption)
    /// Encrypted with key derived from master password
    /// </summary>
    public required string EncryptedMasterKey { get; init; }
    
    /// <summary>
    /// Indicates if user has premium subscription
    /// </summary>
    public bool IsPremium { get; init; }
    
    /// <summary>
    /// Premium subscription expiry date (UTC)
    /// </summary>
    public DateTime? PremiumExpiresAtUtc { get; init; }
    
    /// <summary>
    /// Account creation timestamp
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLoginUtc { get; init; }
    
    /// <summary>
    /// Email verification status
    /// </summary>
    public bool EmailVerified { get; init; }
    
    /// <summary>
    /// Two-factor authentication enabled
    /// </summary>
    public bool TwoFactorEnabled { get; init; }
    
    /// <summary>
    /// Encrypted 2FA secret (TOTP)
    /// </summary>
    public string? EncryptedTwoFactorSecret { get; init; }
    
    /// <summary>
    /// Account locked status (after failed login attempts)
    /// </summary>
    public bool IsLocked { get; init; }
    
    /// <summary>
    /// Failed login attempt count
    /// </summary>
    public int FailedLoginAttempts { get; init; }
    
    /// <summary>
    /// Last failed login timestamp
    /// </summary>
    public DateTime? LastFailedLoginUtc { get; init; }
}