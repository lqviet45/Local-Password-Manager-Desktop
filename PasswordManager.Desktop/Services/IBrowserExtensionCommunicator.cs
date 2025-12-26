namespace PasswordManager.Desktop.Services;

/// <summary>
/// Interface for communication between desktop app and browser extensions (Desktop-only).
/// Uses Native Messaging API for secure bidirectional communication.
/// This service is NOT needed by the API backend.
/// 
/// Reference: https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging
/// </summary>
public interface IBrowserExtensionCommunicator : IDisposable
{
    /// <summary>
    /// Starts the native messaging host server.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the native messaging host server.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the communicator is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Registers the native messaging manifest for a specific browser.
    /// </summary>
    Task<bool> RegisterManifestAsync(BrowserType browser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters the native messaging manifest for a specific browser.
    /// </summary>
    Task<bool> UnregisterManifestAsync(BrowserType browser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the browser extension.
    /// </summary>
    Task<bool> SendMessageAsync(ExtensionMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a message is received from the browser extension.
    /// </summary>
    event EventHandler<ExtensionMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Event raised when a connection is established with the browser extension.
    /// </summary>
    event EventHandler<BrowserConnectedEventArgs>? BrowserConnected;

    /// <summary>
    /// Event raised when the browser extension disconnects.
    /// </summary>
    event EventHandler<BrowserDisconnectedEventArgs>? BrowserDisconnected;
}

#region DTOs and Enums

/// <summary>
/// Supported browser types.
/// </summary>
public enum BrowserType
{
    Chrome,
    Firefox,
    Edge,
    Brave,
    Opera
}

/// <summary>
/// Message types for browser extension communication.
/// </summary>
public enum ExtensionMessageType
{
    GetCredentials,
    SaveCredentials,
    UpdateCredentials,
    DeleteCredentials,
    CheckVaultLocked,
    FillCredentials,
    LockVault,
    UnlockVault,
    Ping,
    Pong
}

/// <summary>
/// Message exchanged between desktop app and browser extension.
/// </summary>
public sealed record ExtensionMessage
{
    public required ExtensionMessageType Type { get; init; }
    public required string MessageId { get; init; }
    public Dictionary<string, object>? Data { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event args for message received event.
/// </summary>
public sealed class ExtensionMessageReceivedEventArgs : EventArgs
{
    public required ExtensionMessage Message { get; init; }
    public required BrowserType Browser { get; init; }
}

/// <summary>
/// Event args for browser connected event.
/// </summary>
public sealed class BrowserConnectedEventArgs : EventArgs
{
    public required BrowserType Browser { get; init; }
    public required string ExtensionId { get; init; }
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event args for browser disconnected event.
/// </summary>
public sealed class BrowserDisconnectedEventArgs : EventArgs
{
    public required BrowserType Browser { get; init; }
    public required string Reason { get; init; }
    public DateTime DisconnectedAt { get; init; } = DateTime.UtcNow;
}

#endregion