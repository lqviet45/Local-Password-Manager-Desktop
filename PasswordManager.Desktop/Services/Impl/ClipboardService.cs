using System.Windows;
using System.Windows.Threading;

namespace PasswordManager.Desktop.Services.Impl;

public sealed class ClipboardService : IClipboardService
{
    private DispatcherTimer? _clearTimer;
    private string? _lastCopiedText;
    private static readonly TimeSpan DefaultClearTimeout = TimeSpan.FromSeconds(30);

    public void CopyToClipboard(string text, TimeSpan? clearAfter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        try
        {
            // Copy to clipboard
            Clipboard.SetText(text);
            _lastCopiedText = text;

            // Setup auto-clear timer
            var timeout = clearAfter ?? DefaultClearTimeout;
            SetupClearTimer(timeout);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to copy to clipboard", ex);
        }
    }

    public void ClearClipboard()
    {
        try
        {
            // Only clear if we were the ones who set the clipboard
            if (_lastCopiedText != null && Clipboard.ContainsText())
            {
                var currentText = Clipboard.GetText();
                if (currentText == _lastCopiedText)
                {
                    Clipboard.Clear();
                }
            }

            _lastCopiedText = null;
            _clearTimer?.Stop();
            _clearTimer = null;
        }
        catch
        {
            // Ignore errors when clearing
        }
    }

    private void SetupClearTimer(TimeSpan timeout)
    {
        // Cancel existing timer
        _clearTimer?.Stop();

        // Create new timer
        _clearTimer = new DispatcherTimer
        {
            Interval = timeout
        };

        _clearTimer.Tick += (s, e) =>
        {
            ClearClipboard();
        };

        _clearTimer.Start();
    }
}