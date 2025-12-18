using PasswordManager.Domain.ValueObjects;

namespace PasswordManager.Domain.Interfaces;

/// <summary>
/// Interface for cryptographic operations.
/// Implementations must use Argon2id for key derivation and AES-256-GCM for encryption.
/// </summary>
public interface ICryptoProvider
{
    /// <summary>
    /// Derives a key from a password using Argon2id.
    /// </summary>
    /// <param name="password">Master password</param>
    /// <param name="salt">Salt (will be generated if null)</param>
    /// <param name="keySize">Key size in bytes (default: 32 for AES-256)</param>
    /// <returns>Derived key and salt used</returns>
    Task<(byte[] Key, byte[] Salt)> DeriveKeyAsync(string password, byte[]? salt = null, int keySize = 32);
    
    /// <summary>
    /// Encrypts plaintext using AES-256-GCM.
    /// </summary>
    /// <param name="plaintext">Data to encrypt</param>
    /// <param name="key">256-bit encryption key</param>
    /// <returns>Encrypted data with IV and authentication tag</returns>
    Task<EncryptedData> EncryptAsync(string plaintext, byte[] key);
    
    /// <summary>
    /// Decrypts ciphertext using AES-256-GCM.
    /// </summary>
    /// <param name="encryptedData">Encrypted data with IV and tag</param>
    /// <param name="key">256-bit decryption key</param>
    /// <returns>Decrypted plaintext</returns>
    Task<string> DecryptAsync(EncryptedData encryptedData, byte[] key);
    
    /// <summary>
    /// Hashes a password using Argon2id for storage.
    /// </summary>
    /// <param name="password">Password to hash</param>
    /// <param name="salt">Salt (will be generated if null)</param>
    /// <returns>Password hash string (includes algorithm parameters)</returns>
    Task<string> HashPasswordAsync(string password, byte[]? salt = null);
    
    /// <summary>
    /// Verifies a password against its hash.
    /// </summary>
    /// <param name="password">Password to verify</param>
    /// <param name="hash">Stored password hash</param>
    /// <returns>True if password matches hash</returns>
    Task<bool> VerifyPasswordAsync(string password, string hash);
    
    /// <summary>
    /// Generates a cryptographically secure random key.
    /// </summary>
    /// <param name="keySize">Key size in bytes</param>
    /// <returns>Random key</returns>
    byte[] GenerateRandomKey(int keySize = 32);
    
    /// <summary>
    /// Computes SHA-256 hash (for integrity checks).
    /// </summary>
    /// <param name="data">Data to hash</param>
    /// <returns>Base64-encoded hash</returns>
    string ComputeHash(string data);
}