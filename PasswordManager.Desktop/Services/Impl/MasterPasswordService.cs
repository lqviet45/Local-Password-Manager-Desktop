using PasswordManager.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Service for managing master password and derived encryption keys.
/// DEBUGGING VERSION with extensive logging
/// </summary>
public sealed class MasterPasswordService : IMasterPasswordService, IDisposable
{
    private readonly ICryptoProvider _cryptoProvider;
    private readonly ILogger<MasterPasswordService> _logger;
    private byte[]? _encryptionKey;
    private byte[]? _masterKey;
    private byte[]? _salt;
    private bool _isInitialized;
    private readonly object _lock = new();

    public MasterPasswordService(
        ICryptoProvider cryptoProvider,
        ILogger<MasterPasswordService> logger)
    {
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("MasterPasswordService created");
    }

    public bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _isInitialized;
            }
        }
    }

    public async Task InitializeAsync(string masterPassword, byte[] userSalt, string? encryptedMasterKey = null)
    {
        _logger.LogInformation("=== InitializeAsync CALLED ===");
        _logger.LogInformation("Master password length: {Length}", masterPassword?.Length ?? 0);
        _logger.LogInformation("User salt provided: {HasSalt}", userSalt != null);
        _logger.LogInformation("User salt length: {Length} bytes", userSalt?.Length ?? 0);

        ArgumentException.ThrowIfNullOrWhiteSpace(masterPassword);
        ArgumentNullException.ThrowIfNull(userSalt);

        if (userSalt.Length != 32)
        {
            _logger.LogError("❌ Invalid salt length! Expected 32 bytes, got {Length}", userSalt.Length);
            throw new ArgumentException($"Salt must be 32 bytes, got {userSalt.Length}", nameof(userSalt));
        }

        _logger.LogInformation("Salt validation passed ✓");
        _logger.LogInformation("Salt (first 16 bytes): {SaltHex}", Convert.ToHexString(userSalt[..16]));

        lock (_lock)
        {
            if (_isInitialized)
            {
                _logger.LogWarning("Service already initialized, clearing previous data");
                ClearSensitiveDataInternal();
            }
        }

        try
        {
            _logger.LogInformation("Calling CryptoProvider.DeriveKeyAsync...");
            _logger.LogInformation("  - Password: {PasswordIndicator}", 
                string.IsNullOrEmpty(masterPassword) ? "EMPTY" : $"[{masterPassword.Length} chars]");
            _logger.LogInformation("  - Salt: {SaltIndicator}", 
                userSalt == null ? "NULL" : $"[{userSalt.Length} bytes]");

            // ✅ CRITICAL: Use provided salt (from database) to derive key
            var (key, salt) = await _cryptoProvider.DeriveKeyAsync(masterPassword, userSalt);

            _logger.LogInformation("✓ Key derivation completed");
            _logger.LogInformation("  - Derived key length: {Length} bytes", key?.Length ?? 0);
            _logger.LogInformation("  - Derived key (first 16 bytes): {KeyHex}", 
                key != null && key.Length >= 16 ? Convert.ToHexString(key[..16]) : "NULL or TOO SHORT");
            _logger.LogInformation("  - Returned salt length: {Length} bytes", salt?.Length ?? 0);
            _logger.LogInformation("  - Salts match: {Match}", 
                userSalt.SequenceEqual(salt ?? Array.Empty<byte>()));

            lock (_lock)
            {
                _encryptionKey = key;
                _salt = salt;
                _isInitialized = true;
            }

            if (!string.IsNullOrWhiteSpace(encryptedMasterKey))
            {
                try
                {
                    var encrypted = Domain.ValueObjects.EncryptedData.FromCombinedString(encryptedMasterKey);
                    var decryptedMasterKeyBase64 = await _cryptoProvider.DecryptAsync(encrypted, key);
                    _masterKey = Convert.FromBase64String(decryptedMasterKeyBase64);
                    _logger.LogInformation("✓ Master key decrypted and cached");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt master key; continuing with derived key only");
                }
            }

            _logger.LogInformation("✓ MasterPasswordService initialized successfully");
            _logger.LogInformation("  - IsInitialized: {IsInitialized}", _isInitialized);
            _logger.LogInformation("  - Encryption key stored: {HasKey}", _encryptionKey != null);
            _logger.LogInformation("  - Salt stored: {HasSalt}", _salt != null);
            _logger.LogInformation("  - Master key stored: {HasMasterKey}", _masterKey != null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize master password service");
            ClearSensitiveData();
            throw new InvalidOperationException("Failed to initialize master password", ex);
        }

        _logger.LogInformation("=== InitializeAsync COMPLETED ===");
    }

    public byte[] GetEncryptionKey()
    {
        _logger.LogDebug("GetEncryptionKey called");

        lock (_lock)
        {
            if (!_isInitialized)
            {
                _logger.LogError("❌ GetEncryptionKey called but service not initialized!");
                throw new InvalidOperationException("Master password not initialized");
            }

            if (_encryptionKey == null)
            {
                _logger.LogError("❌ Service initialized but encryption key is NULL!");
                throw new InvalidOperationException("Encryption key is null");
            }

            _logger.LogDebug("Returning encryption key copy (length: {Length})", _encryptionKey.Length);
            _logger.LogDebug("Key (first 16 bytes): {KeyHex}", 
                Convert.ToHexString(_encryptionKey[..Math.Min(16, _encryptionKey.Length)]));

            // Return a copy to prevent external modification
            var keyCopy = new byte[_encryptionKey.Length];
            Array.Copy(_encryptionKey, keyCopy, _encryptionKey.Length);
            return keyCopy;
        }
    }

    public async Task<bool> VerifyMasterPasswordAsync(string masterPassword, string storedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(masterPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);

        try
        {
            return await _cryptoProvider.VerifyPasswordAsync(masterPassword, storedHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password verification failed");
            return false;
        }
    }

    public void ClearSensitiveData()
    {
        _logger.LogInformation("ClearSensitiveData called");
        lock (_lock)
        {
            ClearSensitiveDataInternal();
        }
    }

    public byte[] GetPreferredKey()
    {
        lock (_lock)
        {
            if (_masterKey != null)
            {
                var keyCopy = new byte[_masterKey.Length];
                Array.Copy(_masterKey, keyCopy, _masterKey.Length);
                return keyCopy;
            }
        }

        return GetEncryptionKey();
    }

    public byte[]? GetMasterKeyOrDefault()
    {
        lock (_lock)
        {
            if (_masterKey == null) return null;
            var keyCopy = new byte[_masterKey.Length];
            Array.Copy(_masterKey, keyCopy, _masterKey.Length);
            return keyCopy;
        }
    }

    private void ClearSensitiveDataInternal()
    {
        _logger.LogInformation("Clearing sensitive data...");

        // Zero out encryption key
        if (_encryptionKey != null)
        {
            Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
            _encryptionKey = null;
            _logger.LogInformation("  - Encryption key cleared");
        }

        if (_masterKey != null)
        {
            Array.Clear(_masterKey, 0, _masterKey.Length);
            _masterKey = null;
            _logger.LogInformation("  - Master key cleared");
        }

        // Zero out salt
        if (_salt != null)
        {
            Array.Clear(_salt, 0, _salt.Length);
            _salt = null;
            _logger.LogInformation("  - Salt cleared");
        }

        _isInitialized = false;
        _logger.LogInformation("  - IsInitialized set to false");

        // Force garbage collection to clear sensitive data
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        _logger.LogInformation("✓ Sensitive data cleared");
    }

    public void Dispose()
    {
        _logger.LogInformation("MasterPasswordService disposed");
        ClearSensitiveData();
    }
}