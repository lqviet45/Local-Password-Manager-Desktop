namespace PasswordManager.Domain.ValueObjects;

/// <summary>
/// Value object representing encrypted data with associated metadata.
/// Ensures encrypted data always has required components for decryption.
/// </summary>
public class EncryptedData
{
    /// <summary>
    /// Base64-encoded ciphertext
    /// </summary>
    public required string Ciphertext { get; init; }
    
    /// <summary>
    /// Base64-encoded initialization vector (IV) for AES-GCM
    /// </summary>
    public required string IV { get; init; }
    
    /// <summary>
    /// Base64-encoded authentication tag for AES-GCM
    /// Provides integrity and authenticity verification
    /// </summary>
    public required string Tag { get; init; }
    
    /// <summary>
    /// Optional salt used for key derivation
    /// </summary>
    public string? Salt { get; init; }
    
    /// <summary>
    /// Validates that all required components are present
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Ciphertext) &&
               !string.IsNullOrWhiteSpace(IV) &&
               !string.IsNullOrWhiteSpace(Tag);
    }
    
    /// <summary>
    /// Serializes to a combined string format for storage
    /// Format: {IV}:{Tag}:{Ciphertext}
    /// </summary>
    public string ToCombinedString()
    {
        return $"{IV}:{Tag}:{Ciphertext}";
    }
    
    /// <summary>
    /// Parses encrypted data from combined string format
    /// </summary>
    public static EncryptedData FromCombinedString(string combined)
    {
        var parts = combined.Split(':');
        if (parts.Length != 3)
        {
            throw new ArgumentException("Invalid encrypted data format", nameof(combined));
        }
        
        return new EncryptedData
        {
            IV = parts[0],
            Tag = parts[1],
            Ciphertext = parts[2]
        };
    }
}