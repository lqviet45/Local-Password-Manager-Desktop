namespace PasswordManager.Desktop.Services;

/// <summary>
/// Local HTTP API server for browser extension communication.
/// Runs on localhost only, no internet exposure.
/// Follows Interface Segregation Principle (SOLID).
/// </summary>
public interface ILocalApiServer : IDisposable
{
    /// <summary>
    /// Start the server on specified port.
    /// Default port: 7777
    /// </summary>
    Task StartAsync(int port = 7777, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the server gracefully.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if server is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Get the server URL (e.g., http://localhost:7777).
    /// </summary>
    string ServerUrl { get; }

    /// <summary>
    /// Get the current port number.
    /// </summary>
    int Port { get; }
}