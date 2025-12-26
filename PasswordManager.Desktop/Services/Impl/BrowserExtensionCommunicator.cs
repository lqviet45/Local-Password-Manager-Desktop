using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Production implementation of browser extension communicator.
/// Uses Native Messaging API for secure communication with browser extensions.
/// </summary>
public sealed class BrowserExtensionCommunicator : IBrowserExtensionCommunicator
{
    private readonly ILogger<BrowserExtensionCommunicator> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Stream? _stdin;
    private Stream? _stdout;
    private Task? _readTask;
    private bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;

    public event EventHandler<ExtensionMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<BrowserConnectedEventArgs>? BrowserConnected;
    public event EventHandler<BrowserDisconnectedEventArgs>? BrowserDisconnected;

    public BrowserExtensionCommunicator(ILogger<BrowserExtensionCommunicator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isRunning)
        {
            _logger.LogWarning("Native messaging host is already running");
            return;
        }

        try
        {
            _isRunning = true;
            _logger.LogInformation("Starting native messaging host server");

            // Native Messaging uses stdin/stdout for communication
            // The browser launches this executable and communicates via standard streams
            _stdin = Console.OpenStandardInput();
            _stdout = Console.OpenStandardOutput();

            // Start reading messages from stdin
            _readTask = Task.Run(() => ReadMessagesAsync(cancellationToken), cancellationToken);

            _logger.LogInformation("Native messaging host server started (using stdin/stdout)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start native messaging host");
            _isRunning = false;
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isRunning)
        {
            return;
        }

        try
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();

            // Wait for read task to complete
            if (_readTask != null)
            {
                try
                {
                    await Task.WhenAny(_readTask, Task.Delay(1000, cancellationToken));
                }
                catch
                {
                    // Ignore
                }
            }

            _stdin?.Dispose();
            _stdout?.Dispose();
            _stdin = null;
            _stdout = null;

            _logger.LogInformation("Native messaging host server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping native messaging host");
        }
    }

    public async Task<bool> RegisterManifestAsync(BrowserType browser, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var manifestPath = GetManifestPath(browser);
            var manifestContent = GenerateManifest(browser);

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(manifestPath, manifestContent, cancellationToken);

            _logger.LogInformation("Registered native messaging manifest for {Browser} at {Path}", browser, manifestPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register manifest for {Browser}", browser);
            return false;
        }
    }

    public async Task<bool> UnregisterManifestAsync(BrowserType browser, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var manifestPath = GetManifestPath(browser);
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
                _logger.LogInformation("Unregistered native messaging manifest for {Browser}", browser);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister manifest for {Browser}", browser);
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(ExtensionMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isRunning || _stdout == null)
        {
            _logger.LogWarning("Cannot send message: Native messaging host is not running");
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Native Messaging protocol: 4-byte little-endian length prefix
            var lengthBytes = BitConverter.GetBytes(bytes.Length);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }

            await _stdout.WriteAsync(lengthBytes, 0, 4, cancellationToken);
            await _stdout.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            await _stdout.FlushAsync(cancellationToken);

            _logger.LogDebug("Sent message to browser extension: {Type}", message.Type);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to browser extension");
            return false;
        }
    }

    #region Private Methods

    private async Task ReadMessagesAsync(CancellationToken cancellationToken)
    {
        if (_stdin == null)
            return;

        try
        {
            _logger.LogInformation("Browser extension connected via stdin/stdout");

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                // Read message length (4 bytes, little-endian)
                var lengthBytes = new byte[4];
                var totalRead = 0;
                
                while (totalRead < 4)
                {
                    var bytesRead = await _stdin.ReadAsync(lengthBytes, totalRead, 4 - totalRead, cancellationToken);
                    if (bytesRead == 0)
                    {
                        // EOF - browser disconnected
                        _logger.LogInformation("Browser extension disconnected (EOF)");
                        OnBrowserDisconnected(BrowserType.Chrome, "EOF");
                        return;
                    }
                    totalRead += bytesRead;
                }

                // Convert to int (little-endian)
                var messageLength = BitConverter.ToInt32(lengthBytes, 0);
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBytes);
                    messageLength = BitConverter.ToInt32(lengthBytes, 0);
                }

                if (messageLength <= 0 || messageLength > 1024 * 1024) // Max 1MB
                {
                    _logger.LogWarning("Invalid message length: {Length}", messageLength);
                    break;
                }

                // Read message content
                var messageBytes = new byte[messageLength];
                totalRead = 0;
                
                while (totalRead < messageLength)
                {
                    var bytesRead = await _stdin.ReadAsync(messageBytes, totalRead, messageLength - totalRead, cancellationToken);
                    if (bytesRead == 0)
                    {
                        _logger.LogWarning("Unexpected EOF while reading message");
                        return;
                    }
                    totalRead += bytesRead;
                }

                var json = Encoding.UTF8.GetString(messageBytes, 0, totalRead);
                var message = JsonSerializer.Deserialize<ExtensionMessage>(json);

                if (message != null)
                {
                    _logger.LogDebug("Received message from browser extension: {Type}", message.Type);
                    OnMessageReceived(message, BrowserType.Chrome); // TODO: Detect actual browser from manifest
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Read messages cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading messages from browser extension");
            OnBrowserDisconnected(BrowserType.Chrome, ex.Message);
        }
    }

    private void OnMessageReceived(ExtensionMessage message, BrowserType browser)
    {
        MessageReceived?.Invoke(this, new ExtensionMessageReceivedEventArgs
        {
            Message = message,
            Browser = browser
        });
    }

    private void OnBrowserDisconnected(BrowserType browser, string reason)
    {
        BrowserDisconnected?.Invoke(this, new BrowserDisconnectedEventArgs
        {
            Browser = browser,
            Reason = reason
        });
    }

    private string GetManifestPath(BrowserType browser)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return browser switch
        {
            BrowserType.Chrome => Path.Combine(appDataPath, "Google", "Chrome", "User Data", "NativeMessagingHosts", "com.passwordmanager.json"),
            BrowserType.Edge => Path.Combine(appDataPath, "Microsoft", "Edge", "User Data", "NativeMessagingHosts", "com.passwordmanager.json"),
            BrowserType.Brave => Path.Combine(appDataPath, "BraveSoftware", "Brave-Browser", "User Data", "NativeMessagingHosts", "com.passwordmanager.json"),
            _ => Path.Combine(appDataPath, "PasswordManager", "NativeMessagingHosts", $"com.passwordmanager.{browser}.json")
        };
    }

    private string GenerateManifest(BrowserType browser)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "PasswordManager.Desktop.exe";
        
        // For zero-config: Use wildcard pattern that works with any extension ID
        // Chrome/Edge will accept this pattern for development extensions
        var allowedOrigins = browser switch
        {
            BrowserType.Chrome => new[] { "chrome-extension://*/" },
            BrowserType.Edge => new[] { "chrome-extension://*/" },
            BrowserType.Brave => new[] { "chrome-extension://*/" },
            BrowserType.Opera => new[] { "chrome-extension://*/" },
            _ => Array.Empty<string>()
        };

        var manifest = new
        {
            name = "com.passwordmanager",
            description = "Password Manager Native Messaging Host",
            path = exePath,
            type = "stdio",
            allowed_origins = allowedOrigins
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BrowserExtensionCommunicator));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        StopAsync().GetAwaiter().GetResult();
        _cancellationTokenSource.Dispose();
        _stdin?.Dispose();
        _stdout?.Dispose();

        _disposed = true;
    }

    #endregion
}

