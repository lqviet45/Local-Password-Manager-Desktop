using PasswordManager.Domain.Enums;

namespace PasswordManager.Domain.ValueObjects;

/// <summary>
/// Value object representing a password with its hash and strength metrics.
/// </summary>
public class PasswordHash
{
    /// <summary>
    /// Argon2id hash of the password
    /// </summary>
    public required string Hash { get; init; }
    
    /// <summary>
    /// Salt used for hashing (Base64-encoded)
    /// </summary>
    public required string Salt { get; init; }
    
    /// <summary>
    /// Password entropy in bits
    /// </summary>
    public double Entropy { get; init; }
    
    /// <summary>
    /// Strength score (0-100)
    /// </summary>
    public int StrengthScore { get; init; }
    
    /// <summary>
    /// Categorized strength level
    /// </summary>
    public StrengthLevel StrengthLevel { get; init; }
    
    /// <summary>
    /// Indicates if password has been compromised (checked via HIBP)
    /// </summary>
    public bool IsCompromised { get; init; }
    
    /// <summary>
    /// Number of times password appeared in breaches (from HIBP)
    /// </summary>
    public int BreachCount { get; init; }
    
    /// <summary>
    /// Timestamp when hash was created
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}