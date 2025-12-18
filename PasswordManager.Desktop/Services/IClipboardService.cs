namespace PasswordManager.Desktop.Services;

/// <summary>
/// Service for secure clipboard operations.
/// Automatically clears clipboard after timeout.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies text to clipboard with auto-clear timer.
    /// </summary>
    void CopyToClipboard(string text, TimeSpan? clearAfter = null);

    /// <summary>
    /// Immediately clears clipboard.
    /// </summary>
    void ClearClipboard();
}