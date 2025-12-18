namespace PasswordManager.Domain.Entities;

/// <summary>
/// Represents an application user with authentication and subscription details.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// User's email address (used for login)
    /// </summary>
    public required string Email { get; set; }
    
    /// <summary>
    /// Hashed master password (Argon2id)
    /// Never stored in plain text
    /// </summary>
    public required string MasterPasswordHash { get; set; }
    
    /// <summary>
    /// Salt used for master password hashing
    /// </summary>
    public required byte[] Salt { get; set; }
    
    /// <summary>
    /// Encrypted master key (used for vault encryption)
    /// Encrypted with key derived from master password
    /// </summary>
    public required string EncryptedMasterKey { get; set; }
    
    /// <summary>
    /// Indicates if user has premium subscription
    /// </summary>
    public bool IsPremium { get; set; }
    
    /// <summary>
    /// Premium subscription expiry date (UTC)
    /// </summary>
    public DateTime? PremiumExpiresAtUtc { get; set; }
    
    /// <summary>
    /// Account creation timestamp
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLoginUtc { get; set; }
    
    /// <summary>
    /// Email verification status
    /// </summary>
    public bool EmailVerified { get; set; }
    
    /// <summary>
    /// Two-factor authentication enabled
    /// </summary>
    public bool TwoFactorEnabled { get; set; }
    
    /// <summary>
    /// Encrypted 2FA secret (TOTP)
    /// </summary>
    public string? EncryptedTwoFactorSecret { get; set; }
    
    /// <summary>
    /// Account locked status (after failed login attempts)
    /// </summary>
    public bool IsLocked { get; set; }
    
    /// <summary>
    /// Failed login attempt count
    /// </summary>
    public int FailedLoginAttempts { get; set; }
    
    /// <summary>
    /// Last failed login timestamp
    /// </summary>
    public DateTime? LastFailedLoginUtc { get; set; }
}