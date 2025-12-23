using System.Security;

namespace PasswordManager.Desktop.Services;

/// <summary>
/// Service for managing master password and derived encryption keys.
/// Uses Windows DPAPI for in-memory protection.
/// CRITICAL: Master password is NEVER stored on disk.
/// </summary>
public interface IMasterPasswordService
{
    /// <summary>
    /// Initializes the service with user's master password.
    /// Derives encryption key using Argon2id.
    /// </summary>
    Task InitializeAsync(string masterPassword, byte[] userSalt, string? encryptedMasterKey = null);

    /// <summary>
    /// Gets the derived encryption key for vault operations.
    /// </summary>
    byte[] GetEncryptionKey();

    /// <summary>
    /// Returns the preferred key for vault encryption (master key if available, otherwise derived key).
    /// </summary>
    byte[] GetPreferredKey();

    /// <summary>
    /// Returns decrypted master key if available.
    /// </summary>
    byte[]? GetMasterKeyOrDefault();

    /// <summary>
    /// Verifies if the master password is correct.
    /// </summary>
    Task<bool> VerifyMasterPasswordAsync(string masterPassword, string storedHash);

    /// <summary>
    /// Clears all sensitive data from memory (on logout).
    /// </summary>
    void ClearSensitiveData();

    /// <summary>
    /// Indicates if master password has been initialized.
    /// </summary>
    bool IsInitialized { get; }
}