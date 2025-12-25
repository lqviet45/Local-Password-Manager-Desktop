namespace PasswordManager.Desktop.Services;

/// <summary>
/// Interface for auto-saving credentials from browser forms (Desktop-only).
/// Captures login forms and saves credentials to vault.
/// </summary>
public interface IAutoSaveCredentialsService : IDisposable
{
    /// <summary>
    /// Starts monitoring for login forms.
    /// </summary>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring for login forms.
    /// </summary>
    Task StopMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if monitoring is currently active.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Manually captures credentials from the current active window.
    /// </summary>
    Task<bool> CaptureCredentialsAsync(Guid userId, CancellationToken cancellationToken = default);
}

