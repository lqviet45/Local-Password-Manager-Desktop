using PasswordManager.Domain.Interfaces;

namespace PasswordManager.Desktop.Services.Impl;

public sealed class MasterPasswordService : IMasterPasswordService, IDisposable
{
    private readonly ICryptoProvider _cryptoProvider;
    private byte[]? _encryptionKey;
    private byte[]? _salt;
    private bool _isInitialized;
    private readonly object _lock = new();

    public MasterPasswordService(ICryptoProvider cryptoProvider)
    {
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
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

    public async Task InitializeAsync(string masterPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(masterPassword);

        lock (_lock)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Master password already initialized");
            }
        }

        try
        {
            // Derive encryption key from master password
            var (key, salt) = await _cryptoProvider.DeriveKeyAsync(masterPassword);

            lock (_lock)
            {
                _encryptionKey = key;
                _salt = salt;
                _isInitialized = true;
            }

            // TODO: Protect key using Windows DPAPI
            // ProtectedData.Protect(_encryptionKey, null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)
        {
            ClearSensitiveData();
            throw new InvalidOperationException("Failed to initialize master password", ex);
        }
    }

    public byte[] GetEncryptionKey()
    {
        lock (_lock)
        {
            if (!_isInitialized || _encryptionKey == null)
            {
                throw new InvalidOperationException("Master password not initialized");
            }

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
        catch
        {
            return false;
        }
    }

    public void ClearSensitiveData()
    {
        lock (_lock)
        {
            // Zero out encryption key
            if (_encryptionKey != null)
            {
                Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
                _encryptionKey = null;
            }

            // Zero out salt
            if (_salt != null)
            {
                Array.Clear(_salt, 0, _salt.Length);
                _salt = null;
            }

            _isInitialized = false;

            // Force garbage collection to clear sensitive data
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    public void Dispose()
    {
        ClearSensitiveData();
    }
}