using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Production implementation of secure clipboard service.
/// Manages clipboard operations with auto-clear functionality for sensitive data.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> _logger;
    private readonly DispatcherTimer _clearTimer;
    private string? _lastCopiedValue;
    private bool _disposed;

    private const int DefaultClearDelaySeconds = 30;
    private const int MinClearDelaySeconds = 5;
    private const int MaxClearDelaySeconds = 300; // 5 minutes

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _clearTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(DefaultClearDelaySeconds)
        };
        _clearTimer.Tick += OnClearTimerTick;

        _logger.LogInformation(
            "Secure clipboard service initialized with {Delay}s auto-clear",
            DefaultClearDelaySeconds
        );
    }

    #region IClipboardService Implementation

    public bool IsAutoClearActive => _clearTimer.IsEnabled;

    /// <summary>
    /// Copies text to clipboard with auto-clear timer.
    /// BACKWARD COMPATIBLE with existing interface.
    /// </summary>
    public void CopyToClipboard(string text, TimeSpan? clearAfter = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Attempted to copy empty text to clipboard");
            return;
        }

        var delaySeconds = clearAfter.HasValue
            ? (int)clearAfter.Value.TotalSeconds
            : DefaultClearDelaySeconds;

        CopyToClipboardWithAutoClear(text, delaySeconds, "data");
    }

    public void CopyPassword(string password)
    {
        CopyToClipboardWithAutoClear(password, DefaultClearDelaySeconds, "password");
    }

    public void CopyUsername(string username)
    {
        ThrowIfDisposed();

        try
        {
            System.Windows.Clipboard.SetText(username);
            _logger.LogInformation("Copied username to clipboard (no auto-clear)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy username to clipboard");
            throw;
        }
    }

    public void CopyCreditCardNumber(string cardNumber)
    {
        CopyToClipboardWithAutoClear(cardNumber, 15, "credit card number");
    }

    public void CopyCvv(string cvv)
    {
        CopyToClipboardWithAutoClear(cvv, 10, "CVV");
    }

    public void ClearClipboard()
    {
        ThrowIfDisposed();

        try
        {
            _clearTimer.Stop();

            // Only clear if we were the ones who put the data there
            if (_lastCopiedValue != null && System.Windows.Clipboard.ContainsText())
            {
                var currentClipboard = System.Windows.Clipboard.GetText();
                if (currentClipboard == _lastCopiedValue)
                {
                    System.Windows.Clipboard.Clear();
                    _logger.LogInformation("Clipboard cleared manually");
                }
                else
                {
                    _logger.LogDebug("Clipboard content changed externally, skipping clear");
                }
            }

            _lastCopiedValue = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear clipboard");
        }
    }

    public void CancelAutoClear()
    {
        ThrowIfDisposed();

        _clearTimer.Stop();
        _lastCopiedValue = null;
        _logger.LogInformation("Auto-clear cancelled");
    }

    public TimeSpan? GetRemainingTime()
    {
        if (!_clearTimer.IsEnabled)
            return null;

        // Note: DispatcherTimer doesn't expose remaining time directly
        // This returns the configured interval, not actual remaining time
        return _clearTimer.Interval;
    }

    #endregion

    #region Private Methods

    private void CopyToClipboardWithAutoClear(string text, int clearDelaySeconds, string description)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Attempted to copy empty text to clipboard");
            return;
        }

        // Validate delay
        if (clearDelaySeconds < MinClearDelaySeconds || clearDelaySeconds > MaxClearDelaySeconds)
        {
            _logger.LogWarning(
                "Invalid clear delay {Delay}s. Using default {Default}s",
                clearDelaySeconds,
                DefaultClearDelaySeconds
            );
            clearDelaySeconds = DefaultClearDelaySeconds;
        }

        try
        {
            // Stop existing timer
            _clearTimer.Stop();

            // Copy to clipboard
            System.Windows.Clipboard.SetText(text);
            _lastCopiedValue = text;

            // Start auto-clear timer
            _clearTimer.Interval = TimeSpan.FromSeconds(clearDelaySeconds);
            _clearTimer.Start();

            _logger.LogInformation(
                "Copied {Description} to clipboard. Will auto-clear in {Delay} seconds",
                description,
                clearDelaySeconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {Description} to clipboard", description);
            throw;
        }
    }

    private void OnClearTimerTick(object? sender, EventArgs e)
    {
        _logger.LogDebug("Auto-clear timer triggered");
        ClearClipboard();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ClipboardService));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing secure clipboard service");

        _clearTimer.Stop();
        _clearTimer.Tick -= OnClearTimerTick;

        // Clear clipboard on disposal if we put data there
        try
        {
            if (_lastCopiedValue != null && System.Windows.Clipboard.ContainsText())
            {
                var currentClipboard = System.Windows.Clipboard.GetText();
                if (currentClipboard == _lastCopiedValue)
                {
                    System.Windows.Clipboard.Clear();
                    _logger.LogInformation("Clipboard cleared on disposal");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing clipboard during disposal");
        }

        _lastCopiedValue = null;
        _disposed = true;
    }

    #endregion
}