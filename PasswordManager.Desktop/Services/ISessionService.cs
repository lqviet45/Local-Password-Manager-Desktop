using PasswordManager.Domain.Entities;

namespace PasswordManager.Desktop.Services;

/// <summary>
/// Service for managing user session state.
/// Tracks current user, login state, and inactivity timeout.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Current authenticated user.
    /// </summary>
    User? CurrentUser { get; }

    /// <summary>
    /// Indicates if user is logged in.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Starts a session for authenticated user.
    /// </summary>
    void StartSession(User user);

    /// <summary>
    /// Ends current session and clears user data.
    /// </summary>
    void EndSession();

    /// <summary>
    /// Updates last activity timestamp (for auto-lock).
    /// </summary>
    void UpdateLastActivity();

    /// <summary>
    /// Checks if session has timed out.
    /// </summary>
    bool IsSessionTimedOut(TimeSpan timeout);

    /// <summary>
    /// Event fired when session is about to end.
    /// </summary>
    event EventHandler? SessionEnding;

    /// <summary>
    /// Event fired when session has ended.
    /// </summary>
    event EventHandler? SessionEnded;
}